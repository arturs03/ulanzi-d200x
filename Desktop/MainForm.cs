using System.Diagnostics;
using System.Text.Json;

namespace D200xDirect.App;

internal sealed partial class MainForm : Form
{
    readonly string profileDirectory;
    readonly NotifyIcon tray;
    DeckProfile profile = new();
    CancellationTokenSource? session;
    Mutex? deviceOwner;
    Task? sessionTask;
    int actionsEnabled;
    bool closing;
    bool pageSent;
    bool controllerRunning;
    bool sessionKeepsScreenOn;
    string ProfilePath => Path.Combine(profileDirectory, "profile.json");

    public MainForm(string? configurationDirectory = null)
    {
        profileDirectory = configurationDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "D200XDirect");
        BuildLayout();
        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Open D200X Direct", null, (_, _) => ShowWindow());
        trayMenu.Items.Add("Stop", null, (_, _) => session?.Cancel());
        trayMenu.Items.Add("Exit", null, (_, _) => Close());
        tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "D200X Direct", ContextMenuStrip = trayMenu, Visible = true };
        tray.DoubleClick += (_, _) => ShowWindow();
        start.Click += async (_, _) => await StartSession();
        stop.Click += (_, _) => session?.Cancel();
        sendPage.Click += async (_, _) => await SendPage();
        actions.CheckedChanged += (_, _) =>
        {
            Volatile.Write(ref actionsEnabled, actions.Checked ? 1 : 0);
            if (controllerRunning) UpdateActiveStatus();
        };
        FormClosing += OnClosing;
        FormClosed += (_, _) => { tray.Dispose(); helpTips.Dispose(); session?.Dispose(); };
        Directory.CreateDirectory(profileDirectory);
        if (!File.Exists(ProfilePath))
        {
            var starter = Path.Combine(AppContext.BaseDirectory, "profiles", "starter.json");
            if (File.Exists(starter)) File.Copy(starter, ProfilePath);
            else File.WriteAllText(ProfilePath, JsonSerializer.Serialize(new DeckProfile(), Profiles.JsonOptions));
        }
        ReloadProfile();
        Log("Ready. Update screens applies your layout. Choose Start to use the controller. Actions are off until you enable them.");
    }

    void ShowWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }
    static void OpenText(string path) => Process.Start(new ProcessStartInfo("notepad.exe") { ArgumentList = { path }, UseShellExecute = false });

    void ReloadProfile()
    {
        try
        {
            if (new FileInfo(ProfilePath).Length > 1_048_576) throw new IOException("Profile exceeds 1 MB.");
            var candidate = Profiles.Parse(File.ReadAllText(ProfilePath));
            Volatile.Write(ref profile, candidate);
            RefreshDeck();
            SelectControl(selectedIndex);
            Log($"Loaded profile: {candidate.Name}. Dials: {candidate.Dials.Count}; side buttons: {candidate.SideButtons.Count}. Device display is updated separately.");
        }
        catch (Exception error) { Log($"Profile rejected; current mappings kept. {error.Message}"); }
    }

    void Inspect()
    {
        try
        {
            var devices = HidDevice.Enumerate();
            if (devices.Count == 0) { Log("No D200X detected."); return; }
            foreach (var device in devices) Log($"USB: usage {device.UsagePage}/{device.Usage}, input {device.InputLength}, output {device.OutputLength}, report IDs {string.Join(',', device.InputReportIds)}.");
        }
        catch (Exception error) { Log(error.Message); }
    }

    static HidInterface SelectDevice()
    {
        using var processes = new ProcessList(Process.GetProcessesByName("UlanziDeck"));
        if (processes.Items.Length > 0) throw new IOException("Fully exit Ulanzi Studio through its tray icon first.");
        return HidDevice.Enumerate().SingleOrDefault(d => d.InputLength == 1025 && d.OutputLength == 1025
            && d.InputReportIds.SequenceEqual(new[] { 0 }) && d.OutputReportIds.SequenceEqual(new[] { 0 }))
            ?? throw new IOException("Expected one compatible D200X. Choose Check device to see what Windows detects.");
    }

    void AcquireDevice()
    {
        deviceOwner = new Mutex(false, "Local\\D200xDirectController");
        bool acquired;
        try { acquired = deviceOwner.WaitOne(0); }
        catch (AbandonedMutexException) { acquired = true; }
        if (!acquired) { deviceOwner.Dispose(); deviceOwner = null; throw new IOException("Another direct controller is already using the device."); }
    }

    void ReleaseDevice() { deviceOwner?.ReleaseMutex(); deviceOwner?.Dispose(); deviceOwner = null; }
    void SetBusy(bool busy) { start.Visible = !busy; stop.Visible = busy; start.Enabled = sendPage.Enabled = keepAwake.Enabled = !busy; stop.Enabled = busy; }

    void UpdateActiveStatus() => SetStatus("Active", $"Actions {(actions.Checked ? "on" : "off")} · {(sessionKeepsScreenOn ? "Keeping screen on" : "Screen may return to default")}", true);

    async Task StartSession()
    {
        if (sessionTask is not null) return;
        try
        {
            AcquireDevice();
            var device = SelectDevice();
            var sendKeepAwake = keepAwake.Checked;
            if (sendKeepAwake && !pageSent) throw new IOException("Choose Update screens first, then Start with Keep screen on enabled.");
            session = new CancellationTokenSource();
            var token = session.Token;
            SetBusy(true);
            controllerRunning = true; sessionKeepsScreenOn = sendKeepAwake; UpdateActiveStatus();
            sessionTask = Task.Run(async () =>
            {
                using var stream = HidDevice.Open(device, false);
                using var writer = sendKeepAwake ? HidDevice.Open(device, true) : null;
                var report = new byte[device.InputLength];
                var held = new HashSet<int>();
                async Task ReadInput(CancellationToken readToken)
                {
                while (!readToken.IsCancellationRequested)
                {
                    int length;
                    try { length = await stream.ReadAsync(report, readToken); }
                    catch (OperationCanceledException) when (readToken.IsCancellationRequested) { break; }
                    if (length == 0) throw new IOException("Device disconnected. Reconnect it, then start again.");
                    var input = Protocol.ParseInput(report.AsSpan(0, length));
                    if (input is null) { Log($"Unrecognized input report ({length} bytes)."); continue; }
                    Log($"{input.Kind} {input.Index}: {input.Action}");
                    if (input.Action == "release") { held.Remove(input.Index); continue; }
                    if (input.Action == "press" && !held.Add(input.Index)) continue;
                    if (Volatile.Read(ref actionsEnabled) == 0) continue;
                    var action = Profiles.ActionFor(Volatile.Read(ref profile), input);
                    if (action is null || action.Type == "none") continue;
                    try { await ActionRunner.RunAsync(action, input, readToken); Log($"Ran {action.Type} for {input.Index}."); }
                    catch (OperationCanceledException) when (readToken.IsCancellationRequested) { break; }
                    catch (Exception error) { Log($"Action failed: {error.Message}"); }
                }
                }
                if (writer is null) await ReadInput(token);
                else
                {
                    Log("Keep screen on active. Leave the app open or hide it to the tray. Screen updates run every 5 seconds.");
                    var count = 0;
                    await DeviceSession.RunPairAsync(ReadInput, ct => DeviceSession.KeepAwakeAsync(async writeToken =>
                    {
                        await writer.WriteAsync(Protocol.WindowsReport(Protocol.ImageModePacket(TimeOnly.FromDateTime(DateTime.Now))), writeToken);
                        await writer.FlushAsync(writeToken);
                        count++;
                        if (count == 1 || count % 12 == 0) Log($"Keep-awake sent: {count} commands. Report any flicker or return to the default screen.");
                    }, DeviceSession.KeepAwakeInterval, ct), token);
                }
            }, token);
            await sessionTask;
        }
        catch (OperationCanceledException) when (session?.IsCancellationRequested == true) { }
        catch (Exception error) { pageSent = false; Log($"Controller stopped: {error.Message}"); }
        finally
        {
            sessionTask = null;
            controllerRunning = false;
            session?.Dispose(); session = null;
            ReleaseDevice();
            SetBusy(false);
            SetStatus("Ready", "Choose Start to use the controller. Enable actions allows shortcuts to run.");
        }
    }

    async Task SendPage()
    {
        if (sessionTask is not null) return;
        try
        {
            AcquireDevice();
            var device = SelectDevice();
            var snapshot = Volatile.Read(ref profile);
            Profiles.Validate(snapshot);
            SetBusy(true);
            SetStatus("Updating", "Sending saved labels and icons to the device…");
            session = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var token = session.Token;
            sessionTask = Task.Run(async () =>
            {
                // Build and validate the entire bundle before opening the writable device.
                var bundle = ProfileImages.Bundle(snapshot, profileDirectory);
                using var stream = HidDevice.Open(device, true);
                foreach (var packet in Protocol.DisplayPackets(bundle))
                    await stream.WriteAsync(Protocol.WindowsReport(packet), token);
                await stream.FlushAsync(token);
                Log($"Screen data sent: {snapshot.Name} ({bundle.Length} bytes). Check the physical screens to confirm it applied.");
            }, token);
            await sessionTask;
            pageSent = true;
        }
        catch (Exception error) { pageSent = false; Log($"Screen update stopped: {error.Message}"); }
        finally
        {
            sessionTask = null;
            session?.Dispose(); session = null;
            ReleaseDevice(); SetBusy(false); SetStatus("Ready", pageSent ? "Check the device screens. Enable Keep screen on, then Start." : "Screen update failed. See Activity for details.");
        }
    }

    async void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (closing) return;
        if (sessionTask is null) return;
        e.Cancel = true;
        session?.Cancel();
        try { await sessionTask; } catch { }
        // Let the session continuation release its mutex on the UI thread.
        await Task.Yield();
        closing = true;
        Close();
    }

    void Log(string message)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(() => Log(message)); } catch (InvalidOperationException) { }
            return;
        }
        if (log.Lines.Length > 400) log.Lines = log.Lines.Skip(100).ToArray();
        log.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
    }

    sealed class ProcessList(Process[] items) : IDisposable
    {
        public Process[] Items => items;
        public void Dispose() { foreach (var process in items) process.Dispose(); }
    }
}
