using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace D200xDirect.Providers;

public static class ProviderChecks
{
    public static void RunPure()
    {
        var count = 0;
        void Check(bool value, string name) { if (!value) throw new IOException($"Provider check failed: {name}"); count++; }
        void Reject(Action action, string name)
        {
            try { action(); }
            catch (Exception error) when (error is IOException or ArgumentException or JsonException) { count++; return; }
            throw new IOException($"Provider check failed: {name}");
        }
        var message = """{"protocolVersion":1,"requestId":1,"type":"hello","providerId":"d200x.fixture"}""";
        Check(ProviderDiagnostics.Describe(new TimeoutException("private details")) == "response timed out", "deadline diagnostic");
        Check(ProviderDiagnostics.Describe(new System.Threading.Channels.ChannelClosedException(new JsonException("private details"))) == "invalid provider response", "invalid response diagnostic without private text");
        Check(ProviderContract.Parse<ProviderMessage>(Encoding.UTF8.GetBytes(message)).ProviderId == "d200x.fixture", "hello parses");
        Reject(() => ProviderContract.Parse<ProviderMessage>(Encoding.UTF8.GetBytes(message.Replace("\"requestId\":1", "\"requestId\":1,\"requestId\":2"))), "duplicate JSON fields");
        Reject(() => ProviderContract.Parse<ProviderMessage>(Encoding.UTF8.GetBytes("{\"type\":\"hello\"}")), "missing wire fields");
        Reject(() => ProviderContract.Parse<ProviderMessage>(new byte[] { 255 }), "invalid UTF8");
        Reject(() => ProviderContract.Parse<ProviderMessage>(new byte[ProviderContract.MaximumBytes + 1]), "oversized object");
        Reject(() => ProviderContract.Parse<ProviderMessage>(Encoding.UTF8.GetBytes(new string('[', 17) + "0" + new string(']', 17))), "excessive nesting");
        Reject(() => ProviderContract.Parse<ProviderMessage>(Encoding.UTF8.GetBytes(message.Replace("\"providerId\"", "\"command\""))), "unknown wire fields");
        using (var input = new MemoryStream(Encoding.UTF8.GetBytes("{}\r\n[]\n")))
        {
            var reader = new BoundedFrames(input);
            Check(Encoding.UTF8.GetString(reader.ReadAsync(default).GetAwaiter().GetResult()!) == "{}", "CRLF framing");
            Check(Encoding.UTF8.GetString(reader.ReadAsync(default).GetAwaiter().GetResult()!) == "[]", "multiple frames");
            Check(reader.ReadAsync(default).GetAwaiter().GetResult() is null, "clean EOF");
        }
        foreach (var malformed in new[] { new byte[] { 123 }, new byte[] { 10 }, Enumerable.Repeat((byte)'x', ProviderContract.MaximumBytes + 1).ToArray() })
            Reject(() => new BoundedFrames(new MemoryStream(malformed)).ReadAsync(default).GetAwaiter().GetResult(), "bounded framing rejection");

        var selection = new MetricSelection("test.usage", "fixture.test");
        var values = new ProviderValues(); var now = DateTimeOffset.UtcNow;
        var sample = new ProviderSample { MetricId = selection.MetricId, SourceId = selection.SourceId, Unit = "percent", Value = 0, ObservedAt = now, Status = "ok" };
        values.Replace([sample]);
        Check(values.Get(selection, now, TimeSpan.FromSeconds(5))?.Value == 0, "zero is real data");
        Check(values.Get(selection, now.AddSeconds(6), TimeSpan.FromSeconds(5)) is null, "stale invalidation without new snapshot");
        Check(values.Get(selection, now.AddSeconds(-3), TimeSpan.FromSeconds(5)) is null, "future reading unavailable");
        values.Clear(); Check(values.Get(selection, now, TimeSpan.FromSeconds(5)) is null, "failure invalidation");
        var old = Profiles.Parse("""{"schemaVersion":1,"name":"Old","keys":[{"index":13,"label":"Usage","action":{"type":"open-url","url":"https://example.com"}}]}""");
        var binding = new WidgetBinding { ProviderId = "d200x.system", MetricId = "cpu.usage", SourceId = "windows.system", Unit = "percent", Label = "CPU" };
        var bound = ProfileEditing.UpdateWidgets(old, 13, [binding, binding with { MetricId = "ram.usage", Label = "RAM" }]);
        Check(old.Keys[0].Widgets is null && bound.Keys[0].Action.Url == "https://example.com" && bound.Keys[0].Label == "Usage", "binding preserves old profile and action");
        Check(Profiles.Parse(JsonSerializer.Serialize(bound, Profiles.JsonOptions)).Keys[0].Widgets?.Count == 2, "widget profile round trip");
        Reject(() => ProfileEditing.UpdateWidgets(old, 0, [binding, binding with { MetricId = "ram.usage" }]), "normal key accepts only one value");
        Reject(() => ProfileEditing.UpdateWidgets(old, 13, [binding, binding]), "duplicate widget selection");
        Reject(() => ProfileEditing.UpdateWidgets(old, 13, [binding with { ProviderId = "../escape" }]), "invalid widget ID");
        Check(binding.Format(null) == "CPU --", "missing widget unavailable");
        Check(ProfileEditing.UpdateWidgets(bound, 13, []).Keys[0].Widgets is null, "widget removal preserves key");
        var settings = new SensorLogSettings { Directory = @"C:\fixture", TimeZoneId = TimeZoneInfo.Local.Id, CpuTemperature = "/amdcpu/0/temperature/0", GpuTemperature = "/nvidiagpu/0/temperature/0", GpuHotspot = "/nvidiagpu/0/temperature/1" };
        settings.Validate();
        (settings with { GpuUsage = "/nvidiagpu/0/load/0" }).Validate();
        Check(SensorLogMapping.WithGpuUsage(settings, [new("/nvidiagpu/0/load/0", "GPU Core"), new("/nvidiagpu/1/load/0", "GPU Core")]).GpuUsage == "/nvidiagpu/0/load/0", "GPU load auto-matches the configured GPU");
        Check(SensorLogMapping.WithGpuUsage(settings, [new("/nvidiagpu/0/load/0", "GPU Core"), new("/nvidiagpu/0/load/1", "GPU Core")]) == settings, "ambiguous GPU load is never guessed");
        Check(SensorLogMapping.WithGpuUsage(settings, [new("/nvidiagpu/0/load/0", "GPU Memory")]) == settings, "memory load never substitutes core load");
        Reject(() => (settings with { GpuUsage = "/nvidiagpu/1/load/0" }).Validate(), "GPU load must match temperature GPU");
        Reject(() => (settings with { GpuUsage = "/nvidiagpu/0/temperature/0" }).Validate(), "temperature cannot substitute GPU load");
        Check(ProviderDiagnostics.MonitorStatus([sample with { SourceId = "monitor.local", Status = "unavailable", Value = null, ObservedAt = null, Code = "source-unavailable" }])?.Contains("no readable log for today", StringComparison.Ordinal) == true, "missing log diagnosis");
        Check(ProviderDiagnostics.MonitorStatus([sample with { SourceId = "monitor.local", Status = "unavailable", Value = null, ObservedAt = null, Code = "C:/private/source" }])?.Contains("private", StringComparison.Ordinal) == false, "source diagnostics hide unknown provider data");
        Check(ProviderDiagnostics.MonitorStatus([sample with { SourceId = "monitor.local" }])?.Contains("available", StringComparison.Ordinal) == true, "source recovery diagnosis");
        Check(ProviderContract.Parse<SensorLogSettings>(JsonSerializer.SerializeToUtf8Bytes(settings, ProviderContract.Json)) == settings, "private sensor settings round trip");
        Reject(() => (settings with { Directory = @"\\server\logs" }).Validate(), "network sensor path");
        Reject(() => (settings with { Directory = @"C:\fixture\..\logs" }).Validate(), "sensor path traversal");
        Reject(() => (settings with { TimeZoneId = "fixture.invalid" }).Validate(), "changed log time zone");
        Reject(() => (settings with { GpuHotspot = settings.GpuTemperature }).Validate(), "core cannot substitute hotspot");
        Reject(() => (settings with { GpuHotspot = "/nvidiagpu/1/temperature/1" }).Validate(), "different GPUs cannot form one sensor source");
        Reject(() => (settings with { CpuTemperature = settings.GpuTemperature }).Validate(), "GPU cannot substitute CPU package");
        var sensorHeaders = ",/amdcpu/0/temperature/0,/nvidiagpu/0/temperature/0\r\nTime,\"CPU Package\",\"GPU Core\"";
        Check(SensorLogMetadata.ParseHeaders(sensorHeaders)[1].Name == "GPU Core", "metadata reads exact sensor headers");
        Check(SensorLogMetadata.ParseHeaders(sensorHeaders.Replace("GPU Core", "GPU, \"\"Core\"\""))[1].Name == "GPU, \"Core\"", "CSV escaped names");
        Reject(() => SensorLogMetadata.ParseHeaders(sensorHeaders.Replace("/nvidiagpu/0/temperature/0", "/amdcpu/0/temperature/0")), "ambiguous sensor columns");
        Reject(() => SensorLogMetadata.ParseHeaders(sensorHeaders + "\npartial"), "extra metadata rows");

        var folder = Path.Combine(Path.GetTempPath(), "D200x-provider-pure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var manifest = FixtureManifest();
            var package = Path.Combine(folder, "first"); Directory.CreateDirectory(package);
            File.WriteAllBytes(Path.Combine(package, manifest.Executable), []);
            File.WriteAllText(Path.Combine(package, "plugin.json"), JsonSerializer.Serialize(manifest, ProviderContract.Json));
            Check(ProviderDiscovery.Scan(folder).Providers.Count == 1, "metadata-only discovery");
            Check(!File.Exists(Path.Combine(package, "fixture-started.txt")), "discovery never executes");
            foreach (var executable in new[] { "../outside.exe", "C:\\outside.exe", "\\\\server\\x.exe", "run.cmd", "run.exe --arg" })
                Reject(() => (manifest with { Executable = executable }).Validate(), "executable escape or arguments");
            Reject(() => (manifest with { Metrics = [manifest.Metrics[0], manifest.Metrics[0]] }).Validate(), "duplicate metrics");
            Reject(() => (manifest with { ProtocolVersion = 2 }).Validate(), "incompatible manifest");
            var duplicate = Path.Combine(folder, "second"); Directory.CreateDirectory(duplicate);
            File.WriteAllBytes(Path.Combine(duplicate, manifest.Executable), []);
            File.WriteAllText(Path.Combine(duplicate, "plugin.json"), JsonSerializer.Serialize(manifest, ProviderContract.Json));
            Check(ProviderDiscovery.Scan(folder).Providers.Count == 0, "both duplicate IDs rejected");
        }
        finally { Directory.Delete(folder, true); }
        Console.WriteLine($"PASS: {count} provider parsing, discovery and freshness checks. No provider/hardware was started.");
    }

    public static async Task RunFixturesAsync(string fixtureFolder)
    {
        var fixture = ProviderDiscovery.Load(fixtureFolder);
        if (fixture.Manifest.Id != "d200x.fixture" || !fixture.Manifest.Capabilities.SequenceEqual(new[] { "fixture" }))
            throw new ArgumentException("Provider checks require the purpose-built d200x.fixture package.");
        var root = Path.Combine(Path.GetTempPath(), "D200x-provider-process-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var cases = 0;
        var selection = new MetricSelection("test.usage", "fixture.test");
        try
        {
            foreach (var mode in new[] { "normal", "stderr-flood", "stale", "hang-shutdown", "wrong-id", "wrong-version", "wrong-identity", "duplicate", "malformed", "invalid-utf8", "oversized", "partial", "exit", "orphan", "hang-hello", "hang-snapshot", "future", "invalid-value" })
            {
                var folder = Path.Combine(root, mode); Directory.CreateDirectory(folder);
                File.Copy(Path.Combine(fixture.Directory, fixture.Manifest.Executable), Path.Combine(folder, fixture.Manifest.Executable));
                File.WriteAllText(Path.Combine(folder, "plugin.json"), JsonSerializer.Serialize(fixture.Manifest, ProviderContract.Json));
                File.WriteAllText(Path.Combine(folder, "fixture-mode.txt"), mode);
                ProviderProcess? provider = null;
                var succeeded = false;
                try
                {
                    provider = await ProviderProcess.StartAsync(ProviderDiscovery.Load(folder), default, TimeSpan.FromMilliseconds(1000));
                    var firstStarted = Stopwatch.GetTimestamp();
                    var samples = await provider.SnapshotAsync([selection], default);
                    if (samples.Single().Value != 42) throw new IOException("Fixture returned unexpected value.");
                    if (mode == "stale" && provider.Values.Get(selection, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5)) is not null)
                        throw new IOException("Stale fixture became fresh.");
                    if (mode == "stderr-flood" && provider.DiagnosticBytes < 1_048_576) throw new IOException("Stderr was not drained concurrently.");
                    var next = await provider.SnapshotAsync([selection], default);
                    if (next.Single().Value != 42 || Stopwatch.GetElapsedTime(firstStarted).TotalMilliseconds < 1000 || provider.State != ProviderState.Running)
                        throw new IOException("Early snapshot did not wait for the minimum cadence.");
                    succeeded = true;
                }
                catch (Exception error) when (error is IOException or OperationCanceledException or TimeoutException or System.Threading.Channels.ChannelClosedException)
                {
                    if (mode is "normal" or "stderr-flood" or "stale" or "hang-shutdown") throw;
                    if (mode is "hang-hello" or "hang-snapshot" && (error is not TimeoutException || ProviderDiagnostics.Describe(error) != "response timed out"))
                        throw new IOException("A response deadline was not reported distinctly from cancellation.", error);
                }
                finally { if (provider is not null) await provider.DisposeAsync(); }
                if (succeeded != (mode is "normal" or "stderr-flood" or "stale" or "hang-shutdown")) throw new IOException($"Unexpected fixture outcome: {mode}.");
                foreach (var file in new[] { "fixture-started.txt", "fixture-child.txt" })
                {
                    var path = Path.Combine(folder, file);
                    if (!File.Exists(path)) continue;
                    try
                    {
                        using var process = Process.GetProcessById(int.Parse(File.ReadAllText(path)));
                        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                        await process.WaitForExitAsync(limit.Token);
                    }
                    catch (ArgumentException) { }
                }
                cases++;
            }
            var cancelled = Path.Combine(root, "cancel"); Directory.CreateDirectory(cancelled);
            File.Copy(Path.Combine(fixture.Directory, fixture.Manifest.Executable), Path.Combine(cancelled, fixture.Manifest.Executable));
            File.WriteAllText(Path.Combine(cancelled, "plugin.json"), JsonSerializer.Serialize(fixture.Manifest, ProviderContract.Json));
            File.WriteAllText(Path.Combine(cancelled, "fixture-mode.txt"), "hang-snapshot");
            await using (var provider = await ProviderProcess.StartAsync(ProviderDiscovery.Load(cancelled), default))
            {
                using var token = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
                try { await provider.SnapshotAsync([selection], token.Token); throw new IOException("Cancellation was ignored."); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                if (provider.State != ProviderState.Failed) throw new IOException("Cancelled provider remained active.");
            }
            cases++;
            for (var iteration = 0; iteration < 5; iteration++)
            {
                var normal = ProviderDiscovery.Load(Path.Combine(root, "normal"));
                await using var provider = await ProviderProcess.StartAsync(normal, default);
                await provider.SnapshotAsync([selection], default);
                cases++;
            }
            var cadencePackage = ProviderDiscovery.Load(Path.Combine(root, "normal"));
            int cadencePid;
            await using (var provider = await ProviderProcess.StartAsync(cadencePackage, default))
            {
                cadencePid = provider.ProcessId;
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var previous = (await provider.SnapshotAsync([selection], deadline.Token)).Single().ObservedAt;
                // Deliberately wake early, as rounded Windows timers do; retain one process and source cadence.
                for (var iteration = 0; iteration < 16; iteration++)
                {
                    await Task.Delay(iteration < 3 ? 990 : 1000, deadline.Token);
                    var next = (await provider.SnapshotAsync([selection], deadline.Token)).Single();
                    if (next.Value != 42 || next.ObservedAt - previous < TimeSpan.FromSeconds(1)
                        || provider.State != ProviderState.Running || provider.ProcessId != cadencePid)
                        throw new IOException("Nominal/early timer cadence stopped or oversampled the provider.");
                    previous = next.ObservedAt;
                }
                cases++;
                var pending = provider.SnapshotAsync([selection], deadline.Token);
                try { await provider.SnapshotAsync([selection], deadline.Token); throw new IOException("Concurrent cadence wait admitted a second request."); }
                catch (InvalidOperationException) { }
                await pending; cases++;
                using var cancelledWait = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
                var cancellationStarted = Stopwatch.GetTimestamp();
                try { await provider.SnapshotAsync([selection], cancelledWait.Token); throw new IOException("Cadence wait ignored cancellation."); }
                catch (OperationCanceledException) when (cancelledWait.IsCancellationRequested) { }
                if (Stopwatch.GetElapsedTime(cancellationStarted) >= TimeSpan.FromSeconds(1) || provider.State != ProviderState.Running)
                    throw new IOException("Cancelled cadence wait blocked or invalidated the healthy provider.");
                // Cancel before any request is sent; disposal still owns the child and shutdown.
                cases++;
            }
            try { using var child = Process.GetProcessById(cadencePid); if (!child.HasExited) throw new IOException("Cadence test retained its child."); }
            catch (ArgumentException) { }
            cases++;
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine($"PASS: {cases} Rust/C# process fixture checks, including orphan cleanup. No USB, monitor, market or game access.");
    }

    // Execute the real adapter only against owned synthetic logs; never request Windows/hardware usage.
    public static async Task RunSensorLogAsync(string systemFolder)
    {
        var installed = ProviderDiscovery.Load(systemFolder);
        if (installed.Manifest.Id != "d200x.system" || !installed.Manifest.Capabilities.Contains("read-ohm-log"))
            throw new ArgumentException("Sensor checks require the bundled log-capable system provider.");
        if (TimeZoneInfo.Local.IsAmbiguousTime(DateTime.Now))
        { Console.WriteLine("SKIP: fresh synthetic log integration during the ambiguous DST hour; pure DST rejection checks still apply."); return; }
        var root = Path.Combine(Path.GetTempPath(), "D200x-sensor-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var selected = new[] { new MetricSelection("cpu.temperature", "monitor.local"), new MetricSelection("gpu.temperature", "monitor.local"), new MetricSelection("gpu.hotspot", "monitor.local"), new MetricSelection("gpu.usage", "monitor.local") };
        var settings = new SensorLogSettings { Directory = root, TimeZoneId = TimeZoneInfo.Local.Id, CpuTemperature = "/amdcpu/0/temperature/0", GpuTemperature = "/nvidiagpu/0/temperature/0", GpuHotspot = "/nvidiagpu/0/temperature/1", GpuUsage = "/nvidiagpu/0/load/0" };
        var path = Path.Combine(root, $"OpenHardwareMonitorLog-{DateTime.Now:yyyy-MM-dd}.csv");
        const string header = ",/amdcpu/0/temperature/0,/nvidiagpu/0/temperature/0,/nvidiagpu/0/temperature/1,/nvidiagpu/0/load/0\nTime,\"CPU Package\",\"GPU Core\",\"GPU Hot Spot\",\"GPU Core\"\n";
        var checks = 0; int ownedPid = 0;
        async Task<IReadOnlyList<ProviderSample>> Snapshot(ProviderProcess provider, string? values, DateTime? observed = null)
        {
            await Task.Delay(1100);
            if (values is not null) File.WriteAllText(path, header + (observed ?? DateTime.Now).ToString("MM/dd/yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + "," + values + "\npartial");
            return await provider.SnapshotAsync(selected, default);
        }
        void Check(bool valid) { if (!valid) throw new IOException("Synthetic sensor-log integration failed."); checks++; }
        try
        {
            await using (var provider = await ProviderProcess.StartAsync(installed, default, sensorLog: settings))
            {
                ownedPid = provider.ProcessId;
                var first = await Snapshot(provider, "55,62,78,74");
                Check(first.Select(x => x.Value).SequenceEqual(new double?[] { 55, 62, 78, 74 }) && first.Take(3).All(x => x.Unit == "celsius") && first[3].Unit == "percent");
                Check(first.All(x => x.ObservedAt is { } time && time.Millisecond == 0 && time <= DateTimeOffset.UtcNow));
                var missing = await Snapshot(provider, ",62,NaN,74");
                Check(missing[0].Status == "unavailable" && missing[1].Value == 62 && missing[2].Status == "unavailable" && missing[3].Value == 74);
                var invalidLoad = await Snapshot(provider, "55,62,78,101");
                Check(invalidLoad.Take(3).All(x => x.Status == "ok") && invalidLoad[3].Status == "unavailable");
                var stale = await Snapshot(provider, "55,62,78,74", DateTime.Now.AddSeconds(-30));
                Check(stale.All(x => x.Status == "unavailable" && x.ObservedAt is null));
                var future = await Snapshot(provider, "55,62,78,74", DateTime.Now.AddSeconds(30));
                Check(future.All(x => x.Status == "unavailable"));
                File.WriteAllText(path, header.Replace("/temperature/1", "/temperature/0") + "10/05/2026 12:00:00,55,62,78,74\n");
                Check((await Snapshot(provider, null)).All(x => x.Status == "unavailable"));
                Check((await Snapshot(provider, "55,62,78,74")).All(x => x.Status == "ok"));
                File.Move(path, Path.Combine(root, $"OpenHardwareMonitorLog-{DateTime.Now.AddDays(-1):yyyy-MM-dd}.csv"));
                Check((await Snapshot(provider, null)).All(x => x.Status == "unavailable"));
            }
            try { using var process = Process.GetProcessById(ownedPid); Check(process.HasExited); } catch (ArgumentException) { checks++; }
            await using (var unconfigured = await ProviderProcess.StartAsync(installed, default))
                Check((await unconfigured.SnapshotAsync(selected, default)).All(x => x.Status == "unavailable"));
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine($"PASS: {checks} synthetic CSV/IPC/source-time/cleanup checks. No real hardware readings, monitor or USB access.");
    }

    static ProviderManifest FixtureManifest() => new()
    {
        ManifestVersion = 1, ProtocolVersion = 1, Id = "d200x.fixture", Name = "TEST fixture", Version = "0.1.0",
        Executable = "d200x-fixture.exe", Platform = "windows-x64", Capabilities = ["fixture"],
        Metrics = [new() { Id = "test.usage", Unit = "percent", MinimumIntervalMs = 1000, SourceIds = ["fixture.test"] }]
    };
}
