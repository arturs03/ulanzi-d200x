using System.Text.Json;
using D200xDirect.Providers;

namespace D200xDirect.App;

internal sealed partial class MainForm
{
    readonly bool automaticData;
    readonly InstalledProvider? dataFixtureForCheck;
    readonly Dictionary<string, ProviderValues> previewValues = new(StringComparer.Ordinal);
    readonly System.Windows.Forms.Timer widgetFreshness = new() { Interval = 1000 };
    CancellationTokenSource? dataCancellation;
    Task? dataTask;
    Task? dataRun, dataChangeTask;
    long dataRevision;
    bool dataPaused;
    string SensorSettingsPath => Path.Combine(profileDirectory, "system-sensors.json");

    void RequestDataRefresh()
    {
        if (!automaticData || closing || !IsHandleCreated) return;
        dataPaused = false; dataRevision++; CancelData();
        if (dataChangeTask is not null) return;
        dataChangeTask = ApplyDataChangesAsync(); UpdateStopControl();
    }

    void PauseData()
    {
        dataPaused = true; dataRevision++; CancelData();
    }

    void ResumeData() { if (dataPaused || dataTask is null) RequestDataRefresh(); }

    async Task ApplyDataChangesAsync()
    {
        // Own and serialize changes; rapid edits coalesce before any replacement process is launched.
        await Task.Yield();
        try
        {
            while (!closing && !dataPaused)
            {
                var revision = dataRevision;
                await StopDataAsync();
                if (revision != dataRevision) continue;
                if (closing || dataPaused) return;
                dataRun = CollectAssignedDataAsync();
                return;
            }
        }
        catch (Exception) { Log("Could not update hardware data. Reload file to try again."); }
        finally { dataChangeTask = null; UpdateStopControl(); }
    }

    async Task CollectAssignedDataAsync()
    {
        try { await StartDataAsync(); }
        catch (Exception error) { Log($"Hardware data unavailable: {ProviderDiagnostics.Describe(error)}. Reload file or update the assignment to try again."); }
    }

    static string WidgetSignature(DeckProfile configuration) => JsonSerializer.Serialize(configuration.Keys
        .Where(x => x.Widgets is { Count: > 0 }).OrderBy(x => x.Index).Select(x => new { x.Index, x.Widgets }), Profiles.JsonOptions);

    async Task StartDataAsync()
    {
        if (dataTask is not null) return;
        DiscoverWidgetChoices();
        var bindings = profile.Keys.SelectMany(x => x.Widgets ?? []).Distinct().ToArray();
        if (bindings.Length == 0) return;
        var jobs = new List<(InstalledProvider Provider, MetricSelection[] Selected, int Interval)>();
        SensorLogSettings? sensorLog = null;
        if (bindings.Any(b => b.ProviderId == "d200x.system" && b.SourceId == "monitor.local"))
        {
            try { sensorLog = SensorLogSettings.Load(SensorSettingsPath); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or JsonException) { Log("Temperature source settings are invalid. Use Sensor source… to select them again."); }
            if (sensorLog is null) Log("Hardware monitor source is not configured. Select a Hardware data key, then Sensor source…");
        }
        var unavailable = 0;
        foreach (var group in bindings.GroupBy(x => x.ProviderId))
        {
            if (!installedProviders.TryGetValue(group.Key, out var installed)) { unavailable += group.Count(); continue; }
            var valid = group.Where(binding => installed.Manifest.Metrics.Any(metric => metric.Id == binding.MetricId && metric.Unit == binding.Unit
                && metric.SourceIds.Contains(binding.SourceId) && metric.MinimumIntervalMs <= 5000)).ToArray();
            unavailable += group.Count() - valid.Length;
            if (valid.Length == 0) continue;
            var selected = valid.Select(x => new MetricSelection(x.MetricId, x.SourceId)).Distinct().ToArray();
            var interval = installed.Manifest.Metrics.Where(x => selected.Any(s => s.MetricId == x.Id)).Max(x => x.MinimumIntervalMs);
            jobs.Add((installed, selected, interval));
        }
        if (unavailable > 0) Log($"{unavailable} assigned data value(s) unavailable: provider/source missing or unsupported interval. Assignments are kept.");
        if (jobs.Count == 0) return;
        await RunDataAsync(jobs, sensorLog);
    }

    async Task RunDataAsync(IReadOnlyList<(InstalledProvider Provider, MetricSelection[] Selected, int Interval)> jobs, SensorLogSettings? sensorLog = null)
    {
        dataCancellation = new CancellationTokenSource();
        var token = dataCancellation.Token;
        widgetFreshness.Start();
        Log("Hardware data active for assigned screens. Values preview in this app; physical live refresh is pending.");
        // Workers share one collector per provider. Await each UI delivery so updates cannot queue without bounds.
        dataTask = Task.WhenAll(jobs.Select(job => Task.Run(() => CollectProviderAsync(job.Provider, job.Selected, job.Interval, token, job.Provider.Manifest.Id == "d200x.system" ? sensorLog : null), token)));
        UpdateStopControl();
        try { await dataTask; }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            previewValues.Clear(); widgetFreshness.Stop(); RefreshWidgetValues();
            dataTask = null; dataCancellation.Dispose(); dataCancellation = null;
            UpdateStopControl();
            Log("Hardware data stopped. Assignments are kept.");
        }
    }

    async Task CollectProviderAsync(InstalledProvider installed, MetricSelection[] selected, int interval, CancellationToken token, SensorLogSettings? sensorLog)
    {
        var stage = "startup";
        string? lastMonitorStatus = null;
        try
        {
            if (installed.Manifest.Id == "d200x.system" && selected.Any(s => s.MetricId == "gpu.usage" && s.SourceId == "monitor.local") && sensorLog is { GpuUsage: null } previousSource)
            {
                try
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(3));
                    var columns = await SensorLogMetadata.ReadAsync(previousSource.Directory, deadline.Token);
                    var next = SensorLogMapping.WithGpuUsage(previousSource, columns);
                    if (next != previousSource)
                        await DeliverDataAsync(() =>
                        {
                            if (token.IsCancellationRequested || SensorLogSettings.Load(SensorSettingsPath) != previousSource) return;
                            next.Save(SensorSettingsPath); sensorLog = next;
                            Log("GPU load source matched to the configured GPU and saved automatically.");
                        });
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { await DeliverDataAsync(() => Log("GPU load sensor matching timed out. Choose Sensor source… to select it.")); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or JsonException)
                { await DeliverDataAsync(() => Log("GPU load source could not be matched. Choose Sensor source… to select it.")); }
            }
            await using var provider = await ProviderProcess.StartAsync(installed, token, sensorLog: sensorLog);
            stage = "snapshot";
            while (true)
            {
                var samples = await provider.SnapshotAsync(selected, token);
                await DeliverDataAsync(() =>
                {
                    if (token.IsCancellationRequested) return;
                    if (!previewValues.TryGetValue(installed.Manifest.Id, out var store))
                    { store = new ProviderValues(); previewValues.Add(installed.Manifest.Id, store); }
                    store.Replace(samples); RefreshWidgetValues();
                    if (installed.Manifest.Id == "d200x.system" && ProviderDiagnostics.MonitorStatus(samples) is { } status && status != lastMonitorStatus)
                    { Log(status); lastMonitorStatus = status; }
                });
                await Task.Delay(interval, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            await DeliverDataAsync(() => Log($"{installed.Manifest.Name} stopped during {stage}: {ProviderDiagnostics.Describe(error)}. Reload file or update the assignment to try again."));
        }
        finally
        {
            await DeliverDataAsync(() => { previewValues.Remove(installed.Manifest.Id); RefreshWidgetValues(); });
        }
    }

    Task DeliverDataAsync(Action update)
    {
        var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            BeginInvoke(() =>
            {
                try { if (!IsDisposed) update(); delivered.TrySetResult(); }
                catch (Exception error) { delivered.TrySetException(error); }
            });
        }
        catch (InvalidOperationException) { delivered.TrySetResult(); }
        return delivered.Task;
    }

    void CancelData()
    {
        if (dataTask is null) return;
        dataCancellation?.Cancel(); previewValues.Clear(); RefreshWidgetValues();
    }

    async Task StopDataAsync()
    {
        CancelData();
        var run = dataRun ?? dataTask;
        if (run is not null) { try { await run; } catch (OperationCanceledException) { } }
        if (ReferenceEquals(dataRun, run)) dataRun = null;
        await Task.Yield();
    }

    void RefreshWidgetValues()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var key in profile.Keys.Where(x => x.Widgets is { Count: > 0 }))
        {
            var detail = string.Join(" · ", key.Widgets!.Select(binding => binding.Format(
                previewValues.TryGetValue(binding.ProviderId, out var values)
                    ? values.Get(new(binding.MetricId, binding.SourceId), now, TimeSpan.FromSeconds(5)) : null)));
            var tile = tiles[key.Index];
            if (tile.Detail == detail) continue;
            tile.Detail = detail; tile.Invalidate();
        }
    }

    Dictionary<WidgetBinding, ProviderSample?> CaptureDisplayReadings(DeckProfile snapshot)
    {
        var now = DateTimeOffset.UtcNow;
        return snapshot.Keys.SelectMany(k => k.Widgets ?? []).Distinct().ToDictionary(binding => binding,
            binding => previewValues.TryGetValue(binding.ProviderId, out var values)
                ? values.Get(new(binding.MetricId, binding.SourceId), now, TimeSpan.FromSeconds(5)) : null);
    }

    async Task WaitForDisplayDataAsync(DeckProfile snapshot, CancellationToken token)
    {
        var bindings = snapshot.Keys.SelectMany(k => k.Widgets ?? []).Distinct().ToArray();
        if (bindings.Length == 0) return;
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        while (System.Diagnostics.Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(4))
        {
            token.ThrowIfCancellationRequested();
            var readings = CaptureDisplayReadings(snapshot);
            if (dataChangeTask is null && (dataTask is null || bindings.All(binding =>
                !installedProviders.ContainsKey(binding.ProviderId) || previewValues.ContainsKey(binding.ProviderId)
                && (binding.ProviderId != "d200x.system" || binding.SourceId != "windows.system" || binding.MetricId != "cpu.usage" || readings[binding] is not null)))) return;
            await Task.Delay(100, token);
        }
        // Bounded wait: missing/failed/stale sources are rendered independently as --.
    }

    internal void CheckScreenProbeGuards()
    {
        var previousPage = pageSent; var previousKey = selectedIndex;
        try
        {
            pageSent = false; SelectControl(13);
            if (screenProbe.Enabled) throw new IOException("A selected screen probe is enabled before a page upload.");
            pageSent = true; SelectControl(13);
            if (!screenProbe.Enabled) throw new IOException("Wide LCD screen cannot be selected for the probe.");
            SetBusy(true);
            if (screenProbe.Enabled) throw new IOException("A selected screen probe overlaps active device control.");
            SetBusy(false); SelectControl(17);
            if (screenProbe.Enabled) throw new IOException("A dial was enabled as an LCD screen probe.");
        }
        finally { pageSent = previousPage; SetBusy(false); SelectControl(previousKey); }
    }

    // Isolated --ui-check only: assignment and all samples are synthetic; no provider launches.
    internal void CheckWidgetPreview()
    {
        var before = JsonSerializer.Serialize(profile, Profiles.JsonOptions);
        SelectControl(10);
        keyTypeInput.SelectedItem = "Hardware data";
        metricInputs[0].SelectedItem = metricInputs[0].Items.OfType<WidgetChoice>().Single(x => x.Binding.MetricId == "cpu.usage");
        if (monitorHelp.Visible) throw new IOException("Windows CPU usage incorrectly requires an external monitor.");
        SaveMapping();
        var saved = Profiles.Parse(File.ReadAllText(ProfilePath)).Keys.Single(x => x.Index == 10);
        if (saved.Action.Type != "none" || saved.Widgets?.Single().MetricId != "cpu.usage" || dataTask is not null)
            throw new IOException("Offline display-only data assignment failed or started a provider.");
        SelectControl(10);
        if (KeyType != "Hardware data" || actionInput.SelectedItem is not ActionDescriptor { Id: "none" }) throw new IOException("Data key was loaded as a mandatory action.");
        actionInput.SelectedItem = actionInput.Items.Cast<ActionDescriptor>().Single(x => x.Id == "open-url");
        valueInput.Text = "https://example.com"; SaveMapping();
        saved = Profiles.Parse(File.ReadAllText(ProfilePath)).Keys.Single(x => x.Index == 10);
        if (saved.Widgets?.Single().MetricId != "cpu.usage" || saved.Action.Url != "https://example.com") throw new IOException("Optional data press action lost its display binding.");
        keyTypeInput.SelectedItem = "Label / image"; SaveMapping();
        saved = Profiles.Parse(File.ReadAllText(ProfilePath)).Keys.Single(x => x.Index == 10);
        if (saved.Widgets is not null || saved.Action.Type != "none") throw new IOException("Switching to display-only content retained an action or binding.");

        SelectControl(13); keyTypeInput.SelectedItem = "Hardware data";
        metricInputs[0].SelectedItem = metricInputs[0].Items.OfType<WidgetChoice>().Single(x => x.Binding.MetricId == "cpu.usage");
        metricInputs[1].SelectedItem = metricInputs[1].Items.OfType<WidgetChoice>().Single(x => x.Binding.MetricId == "ram.usage");
        metricInputs[2].SelectedItem = metricInputs[2].Items.OfType<WidgetChoice>().Single(x => x.Binding.MetricId == "gpu.usage");
        if (!monitorHelp.Visible) throw new IOException("GPU load did not show the running-monitor requirement.");
        metricInputs[2].SelectedIndex = 0;
        if (monitorHelp.Visible) throw new IOException("Removing the last monitor-backed value retained its dependency reminder.");
        metricInputs[2].SelectedItem = metricInputs[2].Items.OfType<WidgetChoice>().Single(x => x.Binding.MetricId == "gpu.usage");
        SaveMapping();
        if (tiles[13].Detail != "CPU -- · RAM -- · GPU --") throw new IOException("Disabled widget preview did not show unavailable.");
        var unchanged = File.ReadAllText(ProfilePath);
        metricInputs[1].SelectedItem = metricInputs[1].Items.OfType<WidgetChoice>().Single(x => x.Binding.MetricId == "cpu.usage"); SaveMapping();
        if (File.ReadAllText(ProfilePath) != unchanged) throw new IOException("Duplicate data edit changed the profile.");
        SelectControl(13);
        var store = new ProviderValues(); previewValues.Add("d200x.system", store);
        store.Replace([new ProviderSample { MetricId = "cpu.usage", SourceId = "windows.system", Unit = "percent", Value = 25, ObservedAt = DateTimeOffset.UtcNow, Status = "ok" }]);
        RefreshWidgetValues();
        if (tiles[13].Detail != "CPU 25% · RAM -- · GPU --") throw new IOException("Composite widget invalidated unrelated CPU reading.");
        store.Replace([new ProviderSample { MetricId = "gpu.usage", SourceId = "monitor.local", Unit = "percent", Value = 74, ObservedAt = DateTimeOffset.UtcNow, Status = "ok" }]);
        RefreshWidgetValues();
        if (tiles[13].Detail != "CPU -- · RAM -- · GPU 74%") throw new IOException("GPU load did not render independently.");
        store.Clear(); RefreshWidgetValues();
        if (tiles[13].Detail != "CPU -- · RAM -- · GPU --") throw new IOException("Stopped preview retained a value.");
        previewValues.Clear();
        var missing = new WidgetBinding { ProviderId = "example.missing", MetricId = "sensor.temp", SourceId = "sensor.second", Unit = "celsius", Label = "Custom", Precision = 2 };
        var imported = ProfileEditing.UpdateWidgets(profile, 12, [missing]);
        File.WriteAllText(ProfilePath, JsonSerializer.Serialize(imported, Profiles.JsonOptions)); ReloadProfile();
        SelectControl(12); labelInput.Text = "Sensor"; SaveMapping();
        if (Profiles.Parse(File.ReadAllText(ProfilePath)).Keys.Single(x => x.Index == 12).Widgets?.Single() != missing || dataTask is not null)
            throw new IOException("Editing an unavailable provider lost its exact source, custom label or precision.");
        foreach (var (index, metric) in new[] { (10, "cpu.temperature"), (11, "gpu.temperature"), (12, "gpu.hotspot") })
        {
            SelectControl(index); keyTypeInput.SelectedItem = "Hardware data";
            metricInputs[0].SelectedItem = metricInputs[0].Items.OfType<WidgetChoice>().Single(x => x.Binding.MetricId == metric);
            if (!monitorHelp.Visible) throw new IOException("Temperature selection did not show the running-monitor requirement.");
            SaveMapping();
            var temperature = Profiles.Parse(File.ReadAllText(ProfilePath)).Keys.Single(x => x.Index == index);
            if (temperature.Widgets?.Single() is not { Unit: "celsius", SourceId: "monitor.local" } binding || binding.MetricId != metric
                || temperature.Action.Type != "none" || dataTask is not null) throw new IOException("Offline temperature assignment failed.");
        }
        SelectControl(13); keyTypeInput.SelectedItem = "Hardware data";
        for (var i = 0; i < 3; i++) metricInputs[i].SelectedItem = metricInputs[i].Items.OfType<WidgetChoice>().Single(x => x.Binding.MetricId == new[] { "cpu.temperature", "gpu.temperature", "gpu.hotspot" }[i]);
        SaveMapping();
        store = new ProviderValues(); previewValues.Add("d200x.system", store);
        store.Replace([new ProviderSample { MetricId = "gpu.temperature", SourceId = "monitor.local", Unit = "celsius", Value = 62, ObservedAt = DateTimeOffset.UtcNow, Status = "ok" }]);
        RefreshWidgetValues();
        if (tiles[13].Detail != "CPU -- · GPU 62°C · Hotspot --") throw new IOException("Independent temperature display failed.");
        previewValues.Clear();
        File.WriteAllText(ProfilePath, before); ReloadProfile();
        SelectControl(13);
        if (keyTypeInput.Parent is ScrollableControl fields)
        {
            fields.AutoScrollPosition = new Point(0, 400); SelectControl(10);
            if (fields.AutoScrollPosition.Y != 0) throw new IOException("Selecting a new key retained the previous editor scroll position.");
        }
    }

    internal void ShowDataEditorCheck()
    {
        SelectControl(13); keyTypeInput.SelectedItem = "Hardware data";
        metricInputs[0].SelectedItem = metricInputs[0].Items.OfType<WidgetChoice>().Single(x => x.Binding.MetricId == "cpu.usage");
        metricInputs[1].SelectedItem = metricInputs[1].Items.OfType<WidgetChoice>().Single(x => x.Binding.MetricId == "ram.usage");
        metricInputs[2].SelectedItem = metricInputs[2].Items.OfType<WidgetChoice>().Single(x => x.Binding.MetricId == "gpu.usage");
        SaveMapping();
        if (metricInputs[0].Top >= metricInputs[1].Top || metricInputs[1].Top >= metricInputs[2].Top || metricInputs[2].Top >= actionInput.Top)
            throw new IOException("Data fields are not ordered before the optional press action.");
        if (!monitorHelp.Visible || monitorHelp.Top <= keyTypeInput.Top || monitorHelp.Bottom > metricInputs[0].Top)
            throw new IOException("The running-monitor reminder is not beside the hardware key type.");
        CheckCompactLayout();
    }

    internal async Task CheckDataLifecycleAsync(string fixtureDirectory)
    {
        var installed = ProviderDiscovery.Load(fixtureDirectory);
        if (installed.Manifest.Id != "d200x.fixture" || !installed.Manifest.Capabilities.Contains("fixture"))
            throw new IOException("Desktop data checks require the purpose-built fixture.");
        for (var iteration = 0; iteration < 2; iteration++)
        {
            File.WriteAllText(Path.Combine(fixtureDirectory, "fixture-mode.txt"), "normal");
            var running = RunDataAsync([(installed, [new("test.usage", "fixture.test")], 1000)]);
            try
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                while (!previewValues.TryGetValue("d200x.fixture", out var values)
                    || values.Get(new("test.usage", "fixture.test"), DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5)) is null)
                    await Task.Delay(20, deadline.Token);
                if (dataTask is null || !stop.Visible || !stop.Enabled) throw new IOException("Data session did not expose the global Stop control.");
            }
            finally { await StopDataAsync(); await running; }
            if (dataTask is not null || previewValues.Count != 0 || stop.Visible)
                throw new IOException("Stopping data retained a task, value or disabled control.");
            var pid = int.Parse(File.ReadAllText(Path.Combine(fixtureDirectory, "fixture-started.txt")));
            try { using var process = System.Diagnostics.Process.GetProcessById(pid); if (!process.HasExited) throw new IOException("Desktop data stop left a child running."); }
            catch (ArgumentException) { }
        }

        File.WriteAllText(Path.Combine(fixtureDirectory, "fixture-mode.txt"), "exit");
        await RunDataAsync([(installed, [new("test.usage", "fixture.test")], 1000)]);
        if (dataTask is not null || previewValues.Count != 0 || stop.Visible) throw new IOException("Failed provider retained a desktop session or value.");
        File.WriteAllText(Path.Combine(fixtureDirectory, "fixture-mode.txt"), "normal");
        using var closeCheck = new MainForm(Path.Combine(profileDirectory, "close-check"), automaticData: false);
        closeCheck.Show();
        var closingRun = closeCheck.RunDataAsync([(installed, [new("test.usage", "fixture.test")], 1000)]);
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            while (!closeCheck.previewValues.ContainsKey("d200x.fixture")) await Task.Delay(20, deadline.Token);
            closeCheck.Close();
            while (!closeCheck.IsDisposed) await Task.Delay(20, deadline.Token);
            await closingRun;
            if (closeCheck.dataTask is not null || closeCheck.previewValues.Count != 0) throw new IOException("App exit retained a data session or values.");
        }
        finally { await closeCheck.StopDataAsync(); await closingRun; }
    }
}
