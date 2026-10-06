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
    int actionsEnabled = 1;
    bool closing;
    bool closeReady;
    bool deviceBusy;
    bool pageSent;
    bool controllerRunning;
    bool sessionKeepsScreenOn;
    bool sessionStopRequested;
    string ProfilePath => Path.Combine(profileDirectory, "profile.json");

    public MainForm(string? configurationDirectory = null, bool automaticData = true, D200xDirect.Providers.InstalledProvider? fixtureProvider = null)
    {
        this.automaticData = automaticData;
        if (fixtureProvider is not null && (fixtureProvider.Manifest.Id != "d200x.fixture" || !fixtureProvider.Manifest.Capabilities.SequenceEqual(new[] { "fixture" })))
            throw new ArgumentException("UI checks require the purpose-built fixture provider.");
        dataFixtureForCheck = fixtureProvider;
        profileDirectory = configurationDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "D200XDirect");
        BuildLayout();
        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Open D200X Direct", null, (_, _) => ShowWindow());
        trayMenu.Items.Add("Stop", null, (_, _) => StopRequested());
        trayMenu.Items.Add("Exit", null, (_, _) => Close());
        tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "D200X Direct", ContextMenuStrip = trayMenu, Visible = true };
        tray.DoubleClick += (_, _) => ShowWindow();
        start.Click += async (_, _) => { ResumeData(); await StartSession(); };
        stop.Click += (_, _) => StopRequested();
        sendPage.Click += async (_, _) => await SendPage();
        screenProbe.Click += async (_, _) => await SendScreens(selectedOnly: true);
        actions.CheckedChanged += (_, _) =>
        {
            Volatile.Write(ref actionsEnabled, actions.Checked ? 1 : 0);
            if (controllerRunning) UpdateActiveStatus();
            else if (!deviceBusy) UpdateReadyStatus();
        };
        FormClosing += OnClosing;
        Shown += (_, _) => RequestDataRefresh();
        widgetFreshness.Tick += (_, _) => RefreshWidgetValues();
        FormClosed += (_, _) => { widgetFreshness.Dispose(); tray.Dispose(); helpTips.Dispose(); session?.Dispose(); };
        Directory.CreateDirectory(profileDirectory);
        DiscoverWidgetChoices();
        if (!File.Exists(ProfilePath))
        {
            var starter = Path.Combine(AppContext.BaseDirectory, "profiles", "starter.json");
            if (File.Exists(starter)) File.Copy(starter, ProfilePath);
            else File.WriteAllText(ProfilePath, JsonSerializer.Serialize(new DeckProfile(), Profiles.JsonOptions));
        }
        ReloadProfile();
        UpdateReadyStatus();
        Log("Ready. Assigned hardware data runs automatically. Update screens applies your layout; Start enables controller input and saved actions.");
    }

    void ShowWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }
    void StopRequested() { sessionStopRequested = true; session?.Cancel(); PauseData(); }
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
            RequestDataRefresh();
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
    void SetBusy(bool busy) { deviceBusy = busy; start.Visible = !busy; start.Enabled = sendPage.Enabled = keepAwake.Enabled = !busy; UpdateScreenProbe(); UpdateStopControl(); }
    void UpdateStopControl() => stop.Visible = stop.Enabled = deviceBusy || dataTask is not null || dataChangeTask is not null;
    void UpdateReadyStatus() => SetStatus("Ready", $"Choose Start to use the controller. Actions are {(actions.Checked ? "on" : "off")}.");

    void UpdateActiveStatus() => SetStatus("Active", $"Actions {(actions.Checked ? "on" : "off")} · {(sessionKeepsScreenOn ? "Keeping screen on" : "Screen may return to default")}", true);

    async Task StartSession()
    {
        if (sessionTask is not null) return;
        try
        {
            AcquireDevice();
            var device = SelectDevice();
            var sendKeepAwake = keepAwake.Checked;
            sessionStopRequested = false; session = new CancellationTokenSource();
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
                    Log("Keep screen on active. Leave the app open or hide it to the tray. Keep-awake signals run every 5 seconds; displayed values change when you update screens.");
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
            UpdateReadyStatus();
        }
    }

    Task SendPage() => SendScreens();

    async Task SendScreens(bool selectedOnly = false)
    {
        if (sessionTask is not null) return;
        if (selectedOnly && (!pageSent || selectedIndex > 13)) { Log("Choose Update screens first, then select an LCD key for the one-shot test."); return; }
        try
        {
            AcquireDevice();
            var device = SelectDevice();
            var snapshot = Volatile.Read(ref profile);
            Profiles.Validate(snapshot);
            var selectedKey = selectedIndex;
            ResumeData();
            SetBusy(true);
            SetStatus("Updating", selectedOnly ? "Testing one selected screen…" : "Preparing a snapshot of current values…");
            sessionStopRequested = false; session = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var token = session.Token;
            sessionTask = PrepareAndSendScreensAsync(snapshot, device, selectedOnly, selectedKey, token);
            await sessionTask;
            pageSent = true;
        }
        catch (OperationCanceledException) when (sessionStopRequested || closing) { pageSent = false; Log("Screen update canceled."); }
        catch (OperationCanceledException) { pageSent = false; Log("Screen update timed out after 30 seconds. Check the device, then try Update screens again."); }
        catch (Exception error) { pageSent = false; Log($"Screen update stopped: {error.Message}"); }
        finally
        {
            sessionTask = null;
            session?.Dispose(); session = null;
            ReleaseDevice(); SetBusy(false); SetStatus("Ready", pageSent ? "Snapshot sent. Automatic physical refresh needs verification." : sessionStopRequested ? "Screen update canceled. Choose Update screens to try again." : "Screen update failed. See Activity for details.");
        }
    }

    async Task PrepareAndSendScreensAsync(DeckProfile snapshot, HidInterface device, bool selectedOnly, int selectedKey, CancellationToken token)
    {
        await Task.Yield(); // Assign sessionTask before pending data or transfer work begins.
        await WaitForDisplayDataAsync(snapshot, token);
        var readings = CaptureDisplayReadings(snapshot);
        var saved = snapshot.Keys.FirstOrDefault(k => k.Index == selectedKey) ?? new KeyConfig { Index = selectedKey };
        var probe = new KeyConfig { Index = saved.Index, Label = ("TEST " + saved.Label)[..Math.Min(64, 5 + saved.Label.Length)],
            Background = saved.Background, Icon = saved.Icon, Widgets = saved.Widgets, Action = saved.Action };
        await Task.Run(async () =>
        {
            // Build/validate before opening the writable device. Profiles never receive runtime values.
            var bundle = selectedOnly ? ProfileImages.SelectedKeyBundle(probe, profileDirectory, readings)
                : ProfileImages.Bundle(snapshot, profileDirectory, readings);
            var packets = (selectedOnly ? Protocol.KeyImagePackets(bundle) : Protocol.DisplayPackets(bundle)).ToArray();
            token.ThrowIfCancellationRequested();
            using var stream = HidDevice.Open(device, true);
            foreach (var packet in packets) await stream.WriteAsync(Protocol.WindowsReport(packet), token);
            await stream.FlushAsync(token);
            Log(selectedOnly
                ? $"One-shot key {selectedKey:D2} test sent ({bundle.Length} bytes, {packets.Length} packets). Confirm its TEST label and values; other screens should stay unchanged. No automatic refresh is enabled."
                : $"Screen snapshot sent: {snapshot.Name} ({bundle.Length} bytes). Hardware keys include current values or -- when unavailable. This is a snapshot, not automatic refresh.");
        }, token);
    }

    async void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (closeReady) return;
        if (closing) { e.Cancel = true; return; }
        closing = true;
        sessionStopRequested = true;
        PauseData();
        if (sessionTask is null && dataTask is null && dataChangeTask is null && dataRun is null) return;
        e.Cancel = true;
        session?.Cancel();
        if (sessionTask is { } deviceTask) { try { await deviceTask; } catch { } }
        if (dataChangeTask is { } changeTask) await changeTask;
        await StopDataAsync();
        // Let the session continuation release its mutex on the UI thread.
        await Task.Yield();
        closeReady = true;
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
