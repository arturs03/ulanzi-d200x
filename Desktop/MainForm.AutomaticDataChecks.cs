using System.Diagnostics;
using System.Text.Json;
using D200xDirect.Providers;

namespace D200xDirect.App;

internal sealed partial class MainForm
{
    // Exercise production demand/lifetime handling with only a purpose-built provider and isolated profiles.
    internal async Task CheckAutomaticDataAsync(string fixtureDirectory)
    {
        var original = ProviderDiscovery.Load(fixtureDirectory);
        var folder = Path.Combine(profileDirectory, "automatic-check-" + Guid.NewGuid().ToString("N"));
        var package = Path.Combine(folder, "fixture"); Directory.CreateDirectory(package);
        File.Copy(Path.Combine(original.Directory, original.Manifest.Executable), Path.Combine(package, original.Manifest.Executable));
        File.WriteAllText(Path.Combine(package, "plugin.json"), JsonSerializer.Serialize(original.Manifest, ProviderContract.Json));
        var installed = ProviderDiscovery.Load(package);
        File.WriteAllText(Path.Combine(folder, "profile.json"), JsonSerializer.Serialize(new DeckProfile(), Profiles.JsonOptions));
        File.WriteAllText(Path.Combine(package, "fixture-mode.txt"), "normal");
        using var check = new MainForm(folder, fixtureProvider: installed);
        int ReadPid() => int.Parse(File.ReadAllText(Path.Combine(package, "fixture-started.txt")));
        void Exited(int pid)
        {
            try { using var process = Process.GetProcessById(pid); if (!process.HasExited) throw new IOException("Automatic collection retained an owned child."); }
            catch (ArgumentException) { }
        }
        async Task Wait(Func<bool> condition)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            while (!condition()) await Task.Delay(20, deadline.Token);
        }
        bool Reading() => check.previewValues.TryGetValue("d200x.fixture", out var values)
            && values.Get(new("test.usage", "fixture.test"), DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5)) is not null;
        void Assign(int index, bool data)
        {
            check.SelectControl(index); check.keyTypeInput.SelectedItem = data ? "Hardware data" : "Label / image";
            if (data) check.metricInputs[0].SelectedItem = check.metricInputs[0].Items.OfType<WidgetChoice>().Single(x => x.Binding.ProviderId == "d200x.fixture");
            check.SaveMapping();
        }
        void SaveProfile(DeckProfile configuration) => File.WriteAllText(check.ProfilePath, JsonSerializer.Serialize(configuration, Profiles.JsonOptions));
        var binding = new WidgetBinding { ProviderId = "d200x.fixture", MetricId = "test.usage", SourceId = "fixture.test", Unit = "percent" };
        try
        {
            check.Show(); await Wait(() => check.dataChangeTask is null);
            if (File.Exists(Path.Combine(package, "fixture-started.txt"))) throw new IOException("An unused provider started.");
            Assign(10, true); await Wait(Reading); var firstPid = ReadPid();
            if (!check.stop.Visible || !check.stop.Enabled || check.profile.Keys.Single(k => k.Index == 10).Action.Type != "none")
                throw new IOException("Automatic display-only assignment or global Stop failed.");
            check.labelInput.Text = "Updated label"; check.SaveMapping(); await Task.Delay(100);
            if (ReadPid() != firstPid) throw new IOException("Appearance changes restarted collection.");
            Assign(11, true); await Wait(Reading); var sharedPid = ReadPid(); Exited(firstPid);
            if (check.previewValues.Count != 1 || check.profile.Keys.Count(k => k.Widgets is { Count: > 0 }) != 2)
                throw new IOException("Assigned keys did not share collection.");
            check.stop.PerformClick(); await Wait(() => check.dataTask is null && check.dataChangeTask is null);
            Exited(sharedPid); await Task.Delay(1100);
            if (ReadPid() != sharedPid || check.previewValues.Count != 0 || !check.dataPaused) throw new IOException("Global Stop restarted automatically.");
            check.ResumeData();
            using (var cancelledSnapshot = new CancellationTokenSource())
            {
                cancelledSnapshot.Cancel();
                try { await check.WaitForDisplayDataAsync(check.profile, cancelledSnapshot.Token); throw new IOException("Screen snapshot wait ignored Stop cancellation."); }
                catch (OperationCanceledException) when (cancelledSnapshot.IsCancellationRequested) { }
            }
            using (var snapshotDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(8)))
                await check.WaitForDisplayDataAsync(check.profile, snapshotDeadline.Token);
            var captured = check.CaptureDisplayReadings(check.profile);
            if (!Reading() || captured.Count != 1 || captured.Values.Single()?.Value != 42)
                throw new IOException("A resumed screen snapshot missed its assigned fixture data.");
            var resumedPid = ReadPid();
            Assign(10, false); Assign(11, false); Assign(12, true);
            await Wait(Reading); Exited(resumedPid); var latestPid = ReadPid();
            if (check.profile.Keys.Where(k => k.Widgets is { Count: > 0 }).Single().Index != 12)
                throw new IOException("Rapid edits did not use the latest assignment.");
            Assign(12, false); await Wait(() => check.dataTask is null && check.dataChangeTask is null); Exited(latestPid);
            if (check.previewValues.Count != 0) throw new IOException("Removing all hardware keys retained readings.");
            File.WriteAllText(Path.Combine(package, "fixture-mode.txt"), "exit");
            Assign(10, true); await Wait(() => check.dataChangeTask is null && check.dataRun is { IsCompleted: true });
            var failedPid = ReadPid(); Exited(failedPid); await Task.Delay(1100);
            if (ReadPid() != failedPid || check.dataTask is not null) throw new IOException("A failed provider restarted without a configuration request.");
            File.WriteAllText(Path.Combine(package, "fixture-mode.txt"), "normal");
            check.ReloadProfile(); await Wait(Reading); var closingPid = ReadPid();
            check.Close(); await Wait(() => check.IsDisposed); Exited(closingPid);
            if (check.dataTask is not null || check.previewValues.Count != 0 || check.dataChangeTask is not null) throw new IOException("Exit did not join automatic collection.");
        }
        finally { if (!check.IsDisposed) { check.Close(); await Wait(() => check.IsDisposed); } }

        // Saved hardware bindings start on the next app opening, with no Start data step.
        SaveProfile(new DeckProfile { Keys = [new KeyConfig { Index = 10, Widgets = [binding] }] });
        using var reopened = new MainForm(folder, fixtureProvider: installed);
        if (reopened.dataTask is not null) throw new IOException("Metadata load started a provider before the app was shown.");
        reopened.Show();
        try
        {
            await Wait(() => reopened.previewValues.ContainsKey("d200x.fixture"));
            var startupPid = ReadPid(); reopened.Close(); await Wait(() => reopened.IsDisposed); Exited(startupPid);
        }
        finally { if (!reopened.IsDisposed) { reopened.Close(); await Wait(() => reopened.IsDisposed); } }

        // Closing while an assignment is queued must suppress any launch and complete pending work.
        using var queued = new MainForm(folder, fixtureProvider: installed);
        queued.Show(); queued.Close(); await Wait(() => queued.IsDisposed);
        if (queued.dataTask is not null || queued.dataChangeTask is not null) throw new IOException("Closing a queued assignment leaked work.");
        Console.WriteLine("PASS: automatic saved/startup assignments, shared collection, rapid edits, global Stop, removal, failure without restart loops and exit cleanup. Synthetic provider only.");
    }
}
