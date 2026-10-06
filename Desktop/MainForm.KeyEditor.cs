using D200xDirect.Providers;

namespace D200xDirect.App;

internal sealed partial class MainForm
{
    readonly Label keyTypeCaption = Theme.Label("Key type", 9, Theme.Muted);
    readonly ModernSelect keyTypeInput = Theme.Select();
    readonly ModernSelect[] metricInputs = [Theme.Select(), Theme.Select(), Theme.Select()];
    readonly Label[] metricCaptions = [Theme.Label("Show data", 9, Theme.Muted), Theme.Label("Second value (optional)", 9, Theme.Muted), Theme.Label("Third value (optional)", 9, Theme.Muted)];
    readonly Label dataHelp = Theme.Label("Data runs automatically. Update screens sends a snapshot of current values.", 9, Theme.Muted);
    readonly Label monitorHelp = Theme.Label("Keep Hardware Monitor running with CSV logging enabled. Temperatures and GPU load depend on it.", 9, Theme.Text);
    readonly ModernButton sensorSourceButton = new() { Text = "Sensor source…", Width = 242 };
    readonly List<WidgetChoice> widgetChoices = [];
    readonly Dictionary<string, InstalledProvider> installedProviders = new(StringComparer.Ordinal);
    string KeyType => keyTypeInput.SelectedItem?.ToString() ?? "Label / image";

    void BuildKeyContentFields(FlowLayoutPanel fields)
    {
        fields.Controls.Add(keyTypeCaption); fields.Controls.Add(keyTypeInput); fields.Controls.Add(monitorHelp);
        keyTypeInput.Items.AddRange(["Shortcut", "Website / link", "Application", "Hardware data", "Label / image"]);
        for (var i = 0; i < metricInputs.Length; i++) { fields.Controls.Add(metricCaptions[i]); fields.Controls.Add(metricInputs[i]); }
        fields.Controls.Add(dataHelp);
        fields.Controls.Add(sensorSourceButton);
        sensorSourceButton.Click += (_, _) =>
        {
            SensorLogSettings? settings = null;
            try { settings = SensorLogSettings.Load(SensorSettingsPath); }
            catch (Exception error) when (error is IOException or ArgumentException or System.Text.Json.JsonException) { Log("Saved sensor source needs to be selected again."); }
            using var dialog = new SensorSourceForm(settings);
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Settings is not { } next) return;
            try { next.Save(SensorSettingsPath); if (settings != next) RequestDataRefresh(); Log("Hardware source saved. Fresh log readings update assigned data automatically."); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { Log("Could not save the temperature source. Previous settings are kept."); }
        };
    }

    // Read metadata only. CPU/RAM remain assignable without a running or installed provider.
    void DiscoverWidgetChoices()
    {
        installedProviders.Clear(); widgetChoices.Clear();
        AddSystem("cpu.usage", "CPU usage", "CPU"); AddSystem("ram.usage", "RAM usage", "RAM");
        AddSystem("cpu.temperature", "CPU temperature", "CPU", "celsius", "monitor.local");
        AddSystem("gpu.temperature", "GPU temperature", "GPU", "celsius", "monitor.local");
        AddSystem("gpu.hotspot", "GPU hotspot temperature", "Hotspot", "celsius", "monitor.local");
        AddSystem("gpu.usage", "GPU load", "GPU", "percent", "monitor.local");
        var scans = new[] { Path.Combine(profileDirectory, "plugins"), Path.Combine(AppContext.BaseDirectory, "plugins") }.Select(ProviderDiscovery.Scan).ToArray();
        var providers = scans.SelectMany(x => x.Providers).ToArray();
        var duplicates = providers.GroupBy(x => x.Manifest.Id).Where(x => x.Count() > 1).Select(x => x.Key).ToHashSet();
        var choicesLimited = false;
        foreach (var installed in providers.Where(x => !duplicates.Contains(x.Manifest.Id) && !x.Manifest.Capabilities.Contains("fixture")))
        {
            installedProviders.Add(installed.Manifest.Id, installed);
            foreach (var metric in installed.Manifest.Metrics)
            foreach (var source in metric.SourceIds)
            {
                if (widgetChoices.Count >= 256) { choicesLimited = true; break; }
                var binding = new WidgetBinding { ProviderId = installed.Manifest.Id, MetricId = metric.Id, SourceId = source, Unit = metric.Unit };
                if (widgetChoices.Any(x => SameSelection(x.Binding, binding))) continue;
                widgetChoices.Add(new(binding, $"{installed.Manifest.Name}: {metric.Id} · {source}"));
            }
        }
        var problems = scans.Sum(x => x.Problems.Count) + duplicates.Count;
        if (problems > 0) Log($"{problems} provider entries rejected. Hardware assignments remain editable.");
        if (choicesLimited) Log("Showing the first 256 data choices. Existing assignments are preserved.");
        if (dataFixtureForCheck is { } fixture)
        {
            installedProviders.Add(fixture.Manifest.Id, fixture);
            foreach (var metric in fixture.Manifest.Metrics)
            foreach (var source in metric.SourceIds)
                widgetChoices.Add(new(new WidgetBinding { ProviderId = fixture.Manifest.Id, MetricId = metric.Id, SourceId = source, Unit = metric.Unit }, "Synthetic data fixture"));
        }

        void AddSystem(string id, string title, string label, string unit = "percent", string source = "windows.system") => widgetChoices.Add(new(
            new WidgetBinding { ProviderId = "d200x.system", MetricId = id, SourceId = source, Unit = unit, Label = label }, title));
    }

    void LoadKeyContents()
    {
        var key = selectedIndex <= 13 ? profile.Keys.FirstOrDefault(x => x.Index == selectedIndex) : null;
        keyTypeInput.SelectedItem = key?.Widgets is { Count: > 0 } ? "Hardware data" : key?.Action.Type switch
        { "hotkey" => "Shortcut", "open-url" => "Website / link", "launch" => "Application", _ => "Label / image" };
        for (var i = 0; i < metricInputs.Length; i++)
        {
            var input = metricInputs[i]; input.Items.Clear(); input.Items.Add(i == 0 ? "Choose a value…" : "No value");
            foreach (var choice in widgetChoices) input.Items.Add(choice);
            var binding = key?.Widgets?.ElementAtOrDefault(i);
            if (binding is null) input.SelectedIndex = 0;
            else
            {
                // Retain custom label/precision and missing providers when editing an existing profile.
                var title = widgetChoices.FirstOrDefault(x => SameSelection(x.Binding, binding))?.Title ?? $"{binding.ProviderId}: {binding.MetricId} · {binding.SourceId}";
                var choice = new WidgetChoice(binding, title);
                var match = input.Items.OfType<WidgetChoice>().FirstOrDefault(x => x.Binding == binding);
                if (match is null) { input.Items.Add(choice); match = choice; }
                input.SelectedItem = match;
            }
        }
        UpdateEditorStructure();
    }

    void ConfigureKeyType(bool clear)
    {
        var wasLoading = loadingEditor; loadingEditor = true;
        try
        {
            var actionId = KeyType switch { "Shortcut" => "hotkey", "Website / link" => "open-url", "Application" => "launch", _ => "none" };
            actionInput.SelectedItem = actionInput.Items.Cast<ActionDescriptor>().Single(x => x.Id == actionId);
            ConfigureActionField(clear); UpdateEditorStructure();
        }
        finally { loadingEditor = wasLoading; }
    }

    void UpdateEditorStructure()
    {
        var fields = keyTypeInput.Parent;
        fields?.SuspendLayout();
        try
        {
            var lcd = selectedIndex <= 13;
            var hardware = lcd && KeyType == "Hardware data";
            keyTypeInput.Visible = keyTypeCaption.Visible = lcd;
            for (var i = 0; i < metricInputs.Length; i++) metricInputs[i].Visible = metricCaptions[i].Visible = hardware && (i == 0 || selectedIndex == 13);
            dataHelp.Visible = hardware;
            monitorHelp.Visible = hardware && metricInputs.Take(selectedIndex == 13 ? 3 : 1).Any(input => input.SelectedItem is WidgetChoice { Binding: { ProviderId: "d200x.system", SourceId: "monitor.local" } });
            sensorSourceButton.Visible = hardware;
            UpdateScreenProbe();
            actionInput.Visible = actionCaption.Visible = !lcd || hardware;
            actionCaption.Text = hardware ? "When pressed (optional)" : "Action";
            presetInput.Visible = presetCaption.Visible = actionInput.SelectedItem is ActionDescriptor { Id: "hotkey" };
            actionHelp.Visible = !lcd || (hardware ? actionInput.SelectedItem is not ActionDescriptor { Id: "none" } : KeyType != "Label / image");
            // WinForms can change child z-order as previously hidden native controls create handles.
            // Keep the question order stable: purpose, values, optional press action, appearance.
            for (var i = 0; i < editorFieldOrder.Length; i++) fields?.Controls.SetChildIndex(editorFieldOrder[i], i);
        }
        finally { fields?.ResumeLayout(performLayout: true); }
    }

    void UpdateScreenProbe() => screenProbe.Enabled = !deviceBusy && pageSent && selectedIndex <= 13;

    IReadOnlyList<WidgetBinding> EditedWidgets()
    {
        if (KeyType != "Hardware data") return [];
        if (metricInputs[0].SelectedItem is not WidgetChoice) throw new ArgumentException("Choose what data this key should show.");
        return metricInputs.Take(selectedIndex == 13 ? 3 : 1).Select(x => (x.SelectedItem as WidgetChoice)?.Binding).OfType<WidgetBinding>().ToArray();
    }

    static bool SameSelection(WidgetBinding a, WidgetBinding b) => a.ProviderId == b.ProviderId && a.MetricId == b.MetricId && a.SourceId == b.SourceId && a.Unit == b.Unit;
    sealed record WidgetChoice(WidgetBinding Binding, string Title) { public override string ToString() => Title; }
}
