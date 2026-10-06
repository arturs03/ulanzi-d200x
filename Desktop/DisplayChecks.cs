using System.Buffers.Binary;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Text.Json;
using D200xDirect.Providers;

namespace D200xDirect.App;

internal static class DisplayChecks
{
    public static void Run(string fixtureDirectory)
    {
        var cpu = new WidgetBinding { ProviderId = "d200x.system", MetricId = "cpu.usage", SourceId = "windows.system", Unit = "percent", Label = "CPU" };
        var ram = cpu with { MetricId = "ram.usage", Label = "RAM" };
        var gpu = cpu with { MetricId = "gpu.usage", SourceId = "fixture.gpu", Label = "GPU" };
        var temperature = cpu with { MetricId = "cpu.temperature", SourceId = "monitor.local", Unit = "celsius" };
        var key = new KeyConfig { Index = 10, Label = "CPU temperature", Icon = "builtin:temperature", Widgets = [temperature] };
        var wide = new KeyConfig { Index = 13, Label = "System", Icon = "builtin:usage", Widgets = [cpu, ram, gpu] };
        var snapshot = new DeckProfile { Keys = [key, wide] };
        var before = JsonSerializer.Serialize(snapshot, Profiles.JsonOptions);
        var now = DateTimeOffset.UtcNow;
        ProviderSample Sample(WidgetBinding binding, double value, DateTimeOffset? observed = null) => new()
        {
            MetricId = binding.MetricId, SourceId = binding.SourceId, Unit = binding.Unit,
            Value = value, Status = "ok", ObservedAt = observed ?? now
        };
        var readings = new Dictionary<WidgetBinding, ProviderSample?> { [cpu] = Sample(cpu, 25), [ram] = Sample(ram, 64), [gpu] = Sample(gpu, 99, now.AddSeconds(-30)), [temperature] = Sample(temperature, 57) };
        var unavailable = ProfileImages.Render(key, fixtureDirectory);
        var fresh = ProfileImages.Render(key, fixtureDirectory, readings);
        if (unavailable.SequenceEqual(fresh)) throw new IOException("Hardware readings were omitted from the device image.");
        foreach (var invalid in new[] { Sample(temperature, 57, now.AddSeconds(-30)), Sample(temperature, 57, now.AddSeconds(30)), Sample(cpu, 57), Sample(temperature, 57) with { Status = "unavailable", Value = null, ObservedAt = null } })
        {
            readings[temperature] = invalid;
            if (!ProfileImages.Render(key, fixtureDirectory, readings).SequenceEqual(unavailable)) throw new IOException("Stale, future, wrong-unit or unavailable data reached the image.");
        }
        readings[temperature] = Sample(temperature, 57);
        var widePng = ProfileImages.Render(wide, fixtureDirectory, readings);
        readings[gpu] = null;
        if (!widePng.SequenceEqual(ProfileImages.Render(wide, fixtureDirectory, readings))
            || widePng.SequenceEqual(ProfileImages.Render(wide, fixtureDirectory))) throw new IOException("A missing GPU erased CPU/RAM or rendered stale GPU data.");
        if (IconReferences.InspectPng(widePng) != (196, 196)) throw new IOException("Wide device encoding changed dimensions.");
        var first = ImageNames(ProfileImages.Bundle(snapshot, fixtureDirectory, readings));
        readings[cpu] = Sample(cpu, 26);
        var next = ImageNames(ProfileImages.Bundle(snapshot, fixtureDirectory, readings));
        if (first["3_2"] == next["3_2"] || first["0_2"] != next["0_2"] || first["0_0"] != next["0_0"])
            throw new IOException("Value changes did not update content filenames independently.");
        readings[cpu] = Sample(cpu, 25);
        if (first["3_2"] != ImageNames(ProfileImages.Bundle(snapshot, fixtureDirectory, readings))["3_2"])
            throw new IOException("Identical formatted pixels changed image names.");
        foreach (var target in new[] { key, wide })
        {
            var bundle = ProfileImages.SelectedKeyBundle(target, fixtureDirectory, readings);
            if (bundle.Length > Protocol.MaximumKeyImageBytes || !Protocol.CleanBoundaries(bundle)) throw new IOException("Selected key bundle exceeded limits.");
            using var zip = new ZipArchive(new MemoryStream(bundle));
            var names = ImageNames(bundle);
            if (zip.Entries.Count != 3 || names.Count != 1 || !names.ContainsKey($"{target.Index % 5}_{target.Index / 5}") || !names.Values.Single().StartsWith("Images/", StringComparison.Ordinal))
                throw new IOException("Single key probe includes other screens or the wrong asset path.");
            using var manifest = zip.GetEntry("manifest.json")!.Open(); using var json = JsonDocument.Parse(manifest);
            if (json.RootElement.EnumerateObject().Single().Value.GetProperty("ViewParam")[0].GetProperty("Font").GetString() != "") throw new IOException("Probe text/font is not baked into its image.");
            var packets = Protocol.KeyImagePackets(bundle).ToArray();
            if (packets.Any(p => p.Length != 1024 || Protocol.WindowsReport(p).Length != 1025)
                || BinaryPrimitives.ReadUInt16BigEndian(packets[0].AsSpan(2)) != 0x000d
                || BinaryPrimitives.ReadUInt32LittleEndian(packets[0].AsSpan(4)) != bundle.Length)
                throw new IOException("Single key transfer framing is invalid.");
            var reconstructed = packets[0].Skip(8).Concat(packets.Skip(1).SelectMany(p => p)).Take(bundle.Length).ToArray();
            if (!reconstructed.SequenceEqual(bundle)) throw new IOException("Single key transfer changed its ZIP.");
        }
        Reject(() => Protocol.KeyImagePackets(new byte[Protocol.MaximumKeyImageBytes + 1]).ToArray());
        Reject(() => Protocol.KeyImagePackets(new byte[3000]).ToArray());
        Reject(() => Protocol.KeyImagePackets([]).ToArray());
        Reject(() => ProfileImages.SelectedKeyBundle(new KeyConfig { Index = 14 }));
        if (JsonSerializer.Serialize(snapshot, Profiles.JsonOptions) != before) throw new IOException("Display snapshot mutated a saved profile.");
        using var preview = new Bitmap(600, 220); using var graphics = Graphics.FromImage(preview);
        graphics.Clear(Color.Black);
        using var normalStream = new MemoryStream(fresh); using var normalImage = Image.FromStream(normalStream);
        using var wideStream = new MemoryStream(widePng); using var wideImage = Image.FromStream(wideStream);
        graphics.DrawImage(normalImage, new Rectangle(0, 12, 196, 196));
        graphics.DrawImage(wideImage, new Rectangle(208, 12, 392, 196));
        preview.Save(Path.Combine(AppContext.BaseDirectory, "ui-check-display-values.png"), ImageFormat.Png);
        Console.WriteLine("PASS: device snapshot values, independent stale/future/missing fields, unchanged profiles, content filenames and bounded one-shot key 10/13 framing. Synthetic readings; no USB writes.");
    }

    static void Reject(Action action)
    {
        try { action(); } catch (ArgumentException) { return; }
        throw new IOException("Invalid selected-key transfer was accepted.");
    }

    static Dictionary<string, string> ImageNames(byte[] bundle)
    {
        using var zip = new ZipArchive(new MemoryStream(bundle)); using var stream = zip.GetEntry("manifest.json")!.Open();
        using var json = JsonDocument.Parse(stream);
        return json.RootElement.EnumerateObject().ToDictionary(p => p.Name, p =>
        {
            var path = p.Value.GetProperty("ViewParam")[0].GetProperty("Icon").GetString()!;
            if (zip.GetEntry(path) is null) throw new IOException("Display image is missing.");
            return path;
        });
    }
}
