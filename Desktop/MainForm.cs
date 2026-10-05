using System.Diagnostics;
using System.Text.Json;

namespace D200xDirect.App;

internal sealed class MainForm : Form
{
    readonly string profileDirectory;
    readonly Label status = new() { AutoSize = true, Text = "Stopped — device operation has not been physically verified.", Padding = new Padding(0, 8, 0, 8) };
    readonly TextBox log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Consolas", 10) };
    readonly TableLayoutPanel grid = new() { ColumnCount = 5, RowCount = 3, Dock = DockStyle.Fill, Padding = new Padding(4) };
    readonly Button start = new() { Text = "Start listening", AutoSize = true };
    readonly Button stop = new() { Text = "Stop", AutoSize = true, Enabled = false };
    readonly Button sendPage = new() { Text = "Send profile to device", AutoSize = true };
    readonly CheckBox actions = new() { Text = "Enable configured actions", AutoSize = true, Padding = new Padding(8, 7, 0, 0) };
    readonly NotifyIcon tray;
    readonly Font keyFont = new("Segoe UI", 10, FontStyle.Bold);
    DeckProfile profile = new();
    CancellationTokenSource? session;
    Mutex? deviceOwner;
    Task? sessionTask;
    int actionsEnabled;
    bool closing;
    string ProfilePath => Path.Combine(profileDirectory, "profile.json");

    public MainForm(string? configurationDirectory = null)
    {
        profileDirectory = configurationDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "D200XDirect");
        Text = "D200X Direct • Preview";
        ClientSize = new Size(1020, 740);
        MinimumSize = new Size(850, 640);
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterScreen;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(18) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        layout.Controls.Add(new Label { Text = "D200X Direct", Font = new Font("Segoe UI", 22, FontStyle.Bold), AutoSize = true });
        var controls = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        controls.Controls.AddRange([start, stop, sendPage, actions]);
        AddButton(controls, "Inspect USB", Inspect);
        AddButton(controls, "Hide to tray", Hide);
        layout.Controls.Add(controls);
        layout.Controls.Add(status);
        for (var i = 0; i < 5; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        for (var i = 0; i < 3; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 3));
        layout.Controls.Add(grid);
        var editing = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        AddButton(editing, "Edit profile JSON", () => OpenText(ProfilePath));
        AddButton(editing, "Reload profile", ReloadProfile);
        AddButton(editing, "Open profile folder", () => Process.Start(new ProcessStartInfo(profileDirectory) { UseShellExecute = true }));
        AddButton(editing, "Instructions for your LLM", () => OpenText(Path.Combine(AppContext.BaseDirectory, "docs", "customization.md")));
        layout.Controls.Add(editing);
        layout.Controls.Add(log);
        Controls.Add(layout);
        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Show D200X Direct", null, (_, _) => ShowWindow());
        trayMenu.Items.Add("Stop device control", null, (_, _) => session?.Cancel());
        trayMenu.Items.Add("Exit", null, (_, _) => Close());
        tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "D200X Direct", ContextMenuStrip = trayMenu, Visible = true };
        tray.DoubleClick += (_, _) => ShowWindow();
        start.Click += async (_, _) => await StartSession();
        stop.Click += (_, _) => session?.Cancel();
        sendPage.Click += async (_, _) => await SendPage();
        actions.CheckedChanged += (_, _) => Volatile.Write(ref actionsEnabled, actions.Checked ? 1 : 0);
        FormClosing += OnClosing;
        FormClosed += (_, _) => { tray.Dispose(); session?.Dispose(); keyFont.Dispose(); };
        Directory.CreateDirectory(profileDirectory);
        if (!File.Exists(ProfilePath))
        {
            var starter = Path.Combine(AppContext.BaseDirectory, "profiles", "starter.json");
            if (File.Exists(starter)) File.Copy(starter, ProfilePath);
            else File.WriteAllText(ProfilePath, JsonSerializer.Serialize(new DeckProfile(), Profiles.JsonOptions));
        }
        ReloadProfile();
        Log("Preview: Start listens only. Check Enable configured actions to run your mappings. Display updates are explicit.");
    }

    void AddButton(FlowLayoutPanel parent, string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += (_, _) => { try { action(); } catch (Exception error) { Log(error.Message); } };
        parent.Controls.Add(button);
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
            foreach (Control control in grid.Controls.Cast<Control>().ToArray()) { grid.Controls.Remove(control); control.Dispose(); }
            for (var i = 0; i < 14; i++)
            {
                var key = candidate.Keys.FirstOrDefault(k => k.Index == i) ?? new KeyConfig { Index = i };
                grid.Controls.Add(new Label
                {
                    Text = $"{i:D2}  {(key.Label.Length > 18 ? key.Label[..18] + "…" : key.Label)}\n{key.Action.Type}", Dock = DockStyle.Fill,
                    AutoEllipsis = true,
                    TextAlign = ContentAlignment.MiddleCenter, BackColor = ColorTranslator.FromHtml(key.Background),
                    ForeColor = Color.White, Margin = new Padding(5), Font = keyFont
                }, i % 5, i / 5);
            }
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
            ?? throw new IOException("Expected exactly one D200X with compatible HID reports. Use Inspect USB first.");
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
    void SetBusy(bool busy) { start.Enabled = sendPage.Enabled = !busy; stop.Enabled = busy; }

    async Task StartSession()
    {
        if (sessionTask is not null) return;
        try
        {
            AcquireDevice();
            var device = SelectDevice();
            session = new CancellationTokenSource();
            var token = session.Token;
            SetBusy(true);
            status.Text = "Listening — press buttons and turn dials. Actions run only when enabled.";
            sessionTask = Task.Run(async () =>
            {
                using var stream = HidDevice.Open(device, false);
                var report = new byte[device.InputLength];
                var held = new HashSet<int>();
                while (!token.IsCancellationRequested)
                {
                    int length;
                    try { length = await stream.ReadAsync(report, token); }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                    if (length == 0) throw new IOException("Device disconnected. Reconnect it, then start again.");
                    var input = Protocol.ParseInput(report.AsSpan(0, length));
                    if (input is null) { Log($"Unrecognized input report ({length} bytes)."); continue; }
                    Log($"{input.Kind} {input.Index}: {input.Action}");
                    if (input.Action == "release") { held.Remove(input.Index); continue; }
                    if (input.Action == "press" && !held.Add(input.Index)) continue;
                    if (Volatile.Read(ref actionsEnabled) == 0) continue;
                    var action = Profiles.ActionFor(Volatile.Read(ref profile), input);
                    if (action is null || action.Type == "none") continue;
                    try { ActionRunner.Run(action); Log($"Ran {action.Type} for {input.Index}."); }
                    catch (Exception error) { Log($"Action failed: {error.Message}"); }
                }
            }, token);
            await sessionTask;
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { Log($"Device session stopped: {error.Message}"); }
        finally
        {
            sessionTask = null;
            session?.Dispose(); session = null;
            ReleaseDevice();
            SetBusy(false);
            status.Text = "Stopped — profile remains saved. No automatic reconnect or startup.";
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
            status.Text = "Sending profile images…";
            session = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var token = session.Token;
            sessionTask = Task.Run(async () =>
            {
                // Build and validate the entire bundle before opening the writable device.
                var bundle = ProfileImages.Bundle(snapshot);
                using var stream = HidDevice.Open(device, true);
                foreach (var packet in Protocol.BundlePackets(bundle))
                    await stream.WriteAsync(Protocol.WindowsReport(packet), token);
                await stream.FlushAsync(token);
                Log($"Sent {snapshot.Name} ({bundle.Length} bytes). Confirm the physical display; reopening Studio is expected to restore its page.");
            }, token);
            await sessionTask;
        }
        catch (Exception error) { Log($"Display transfer stopped: {error.Message}"); }
        finally
        {
            sessionTask = null;
            session?.Dispose(); session = null;
            ReleaseDevice(); SetBusy(false); status.Text = "Stopped — verify the display on your device.";
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
