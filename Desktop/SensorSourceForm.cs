using D200xDirect.Providers;

namespace D200xDirect.App;

internal sealed class SensorSourceForm : Form
{
    readonly TextBox folder = Theme.Input();
    readonly ModernSelect[] sensors = [Theme.Select(), Theme.Select(), Theme.Select(), Theme.Select()];
    readonly Label status = Theme.Label("Choose an existing Open Hardware Monitor log folder. Unique temperature sensors are selected automatically.", 9, Theme.Muted);
    readonly ModernButton read = new() { Text = "Read sensors", Width = 180 };
    readonly ModernButton save = new() { Text = "Save source", Width = 180, Enabled = false };
    readonly ModernButton browse = new() { Text = "Browse…", Width = 140 };
    bool reading;
    string? loadedFolder;
    public SensorLogSettings? Settings { get; private set; }

    public SensorSourceForm(SensorLogSettings? current)
    {
        Text = "Hardware sensor source"; BackColor = Theme.Background; ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 10); StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(660, 770); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
        var fields = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(24), AutoScroll = true };
        fields.Controls.Add(Theme.Label("Hardware source", 17, bold: true));
        var help = Theme.Label("Uses an existing monitor's CSV logs for CPU/GPU temperatures and GPU load. Unique sensors are mapped automatically. The monitor must be running with logging enabled; missing or stale data shows --.", 9, Theme.Muted);
        help.MaximumSize = new Size(602, 0); fields.Controls.Add(help);
        fields.Controls.Add(Theme.Label("Log folder", 9, Theme.Muted));
        folder.Width = 420; folder.Text = current?.Directory ?? "";
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) }; row.Controls.Add(folder); row.Controls.Add(browse); fields.Controls.Add(row);
        fields.Controls.Add(read);
        var captions = new[] { "CPU package temperature", "GPU core temperature", "GPU hotspot temperature", "GPU load" };
        for (var i = 0; i < sensors.Length; i++) { fields.Controls.Add(Theme.Label(captions[i], 9, Theme.Muted)); sensors[i].Width = 602; sensors[i].Items.Add("Not configured"); sensors[i].SelectedIndex = 0; fields.Controls.Add(sensors[i]); }
        status.MaximumSize = new Size(602, 0); fields.Controls.Add(status);
        var clock = Theme.Label($"CSV timestamps use this computer's Windows time zone: {TimeZoneInfo.Local.DisplayName}", 9, Theme.Muted); clock.MaximumSize = new Size(602, 0); fields.Controls.Add(clock);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 76, Padding = new Padding(24, 12, 24, 12) }; var cancel = new ModernButton { Text = "Cancel", Width = 140 };
        buttons.Controls.Add(save); buttons.Controls.Add(cancel); Controls.Add(fields); Controls.Add(buttons);
        Shown += async (_, _) => { Theme.DarkCaption(this); if (current is not null) await ReadSensorsAsync(); };
        FormClosing += (_, e) => { if (reading) e.Cancel = true; };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        browse.Click += async (_, _) => { using var picker = new FolderBrowserDialog { Description = "Choose the existing monitor's CSV log folder", UseDescriptionForTitle = true }; if (picker.ShowDialog(this) == DialogResult.OK) { folder.Text = picker.SelectedPath; await ReadSensorsAsync(); } };
        folder.TextChanged += (_, _) => { save.Enabled = false; loadedFolder = null; };
        read.Click += async (_, _) => await ReadSensorsAsync();
        async Task ReadSensorsAsync()
        {
            if (reading) return;
            reading = true; read.Enabled = browse.Enabled = folder.Enabled = save.Enabled = false; cancel.Enabled = false;
            status.Text = "Reading sensor names…";
            try
            {
                var path = Path.GetFullPath(folder.Text.Trim());
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var columns = await Task.Run(() => SensorLogMetadata.ReadAsync(path, deadline.Token), deadline.Token);
                Populate(path, columns, current);
                status.Text = "Unique sensors are selected automatically. Review any remaining choices; core and hotspot must belong to the same GPU.\nLive values require today's fresh log, even when names come from an older header.";
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            { status.Text = "Could not read supported CSV headers. Check the folder and log format."; }
            catch (OperationCanceledException) { status.Text = "Reading sensor names timed out. Check the local log folder and try again."; }
            finally { reading = false; read.Enabled = browse.Enabled = folder.Enabled = cancel.Enabled = true; }
        }
        save.Click += (_, _) =>
        {
            try
            {
                var next = new SensorLogSettings { Directory = loadedFolder ?? throw new ArgumentException("Read the sensor names first."), TimeZoneId = TimeZoneInfo.Local.Id,
                    CpuTemperature = (sensors[0].SelectedItem as SensorColumn)?.Id, GpuTemperature = (sensors[1].SelectedItem as SensorColumn)?.Id, GpuHotspot = (sensors[2].SelectedItem as SensorColumn)?.Id, GpuUsage = (sensors[3].SelectedItem as SensorColumn)?.Id };
                next.Validate(); Settings = next; DialogResult = DialogResult.OK; Close();
            }
            catch (ArgumentException error) { status.Text = error.Message; }
        };
    }

    void Populate(string path, SensorColumn[] columns, SensorLogSettings? current)
    {
        if (!string.Equals(current?.Directory, path, StringComparison.OrdinalIgnoreCase)) current = null;
        var ids = current is null ? SensorLogMapping.Suggest(columns) : new[] { current.CpuTemperature, current.GpuTemperature, current.GpuHotspot, current.GpuUsage };
        for (var i = 0; i < sensors.Length; i++)
        {
            sensors[i].Items.Clear(); sensors[i].Items.Add("Not configured");
            foreach (var column in columns.Where(c => SensorLogMapping.Compatible(c, i))) sensors[i].Items.Add(column);
            sensors[i].SelectedItem = sensors[i].Items.OfType<SensorColumn>().FirstOrDefault(c => c.Id == ids[i]) ?? (object)"Not configured";
        }
        loadedFolder = path; save.Enabled = true;
    }

    internal static void CheckWithFixtures(Form owner, string capturePath)
    {
        using var form = new SensorSourceForm(null); form.Show(owner);
        const string path = @"C:\fixture";
        form.folder.Text = path;
        form.Populate(path, [new("/amdcpu/0/temperature/0", "CPU Package"), new("/nvidiagpu/0/temperature/0", "GPU Core"), new("/nvidiagpu/0/temperature/1", "GPU Hot Spot"), new("/nvidiagpu/1/temperature/1", "GPU Hot Spot")], null);
        if (form.sensors[0].SelectedIndex != 1 || form.sensors[1].SelectedIndex != 0 || form.sensors[2].SelectedIndex != 0) throw new IOException("Ambiguous GPUs were automatically selected.");
        form.sensors[0].SelectedIndex = 0;
        form.save.PerformClick();
        if (form.Settings is not null) throw new IOException("Empty sensor selection was saved.");
        form.sensors[0].SelectedIndex = 1; form.sensors[1].SelectedIndex = 1; form.sensors[2].SelectedIndex = 2;
        form.save.PerformClick();
        if (form.Settings is not null) throw new IOException("Mismatched GPU selection was saved.");
        form.sensors[2].SelectedIndex = 1;
        var previous = new SensorLogSettings { Directory = path, TimeZoneId = TimeZoneInfo.Local.Id, CpuTemperature = "/amdcpu/0/temperature/0", GpuTemperature = "/nvidiagpu/0/temperature/0", GpuHotspot = "/nvidiagpu/0/temperature/1" };
        var columns = form.sensors.SelectMany(s => s.Items.OfType<SensorColumn>()).Distinct().ToArray();
        form.Populate(@"C:\different-fixture", columns, previous);
        if (form.sensors[0].SelectedIndex != 1 || form.sensors[1].SelectedIndex != 0 || form.sensors[2].SelectedIndex != 0) throw new IOException("A different source folder inherited ambiguous GPU selections.");
        form.Populate(path, columns, previous with { CpuTemperature = null });
        if (form.sensors[0].SelectedIndex != 0) throw new IOException("An intentionally unconfigured saved sensor was replaced.");
        form.Populate(path, [new("/intelcpu/1/temperature/2", "CPU Package"), new("/atigpu/2/temperature/3", "GPU Core"), new("/atigpu/2/temperature/4", "GPU Hot Spot"), new("/atigpu/2/temperature/5", "GPU Memory"), new("/atigpu/2/load/0", "GPU Core"), new("/atigpu/2/load/1", "GPU Memory")], null);
        if (form.sensors.Any(s => s.SelectedIndex != 1)) throw new IOException("Unique temperature sensors were not automatically selected.");
        var automaticColumns = form.sensors.SelectMany(s => s.Items.OfType<SensorColumn>()).ToArray();
        form.Populate(path, automaticColumns.Concat(new[] { new SensorColumn("/atigpu/3/load/0", "GPU Core") }).ToArray(), null);
        if (form.sensors.Skip(1).Any(s => s.SelectedIndex != 0)) throw new IOException("GPU load on another GPU was paired automatically.");
        form.Populate(path, [new("/intelcpu/0/temperature/0", "CPU Package"), new("/amdcpu/0/temperature/0", "CPU Package"), new("/atigpu/0/temperature/0", "GPU Core"), new("/atigpu/0/temperature/1", "GPU Core"), new("/atigpu/0/temperature/2", "GPU Memory"), new("/atigpu/0/temperature/invalid", "GPU Hot Spot")], null);
        if (form.sensors.Any(s => s.SelectedIndex != 0)) throw new IOException("Duplicate or unsupported sensors were automatically mapped.");
        form.Populate(path, columns, previous);
        form.status.Text = "Synthetic sensor names for UI verification. No monitor or real readings used.";
        form.PerformLayout();
        using (var image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height)); image.Save(capturePath); }
        form.save.PerformClick();
        if (form.Settings is not { CpuTemperature: "/amdcpu/0/temperature/0", GpuTemperature: "/nvidiagpu/0/temperature/0", GpuHotspot: "/nvidiagpu/0/temperature/1" }) throw new IOException("Exact sensor assignment failed.");
    }

}
