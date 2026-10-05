using System.Drawing.Imaging;

namespace D200xDirect.App;

internal static class IconChecks
{
    public static void Run(string fixtureDirectory)
    {
        var sourcePath = Path.Combine(fixtureDirectory, "sample-icon.png");
        using (var sample = new Bitmap(300, 180))
        {
            using var graphics = Graphics.FromImage(sample); graphics.Clear(Color.Transparent);
            graphics.FillRectangle(Brushes.AliceBlue, 40, 40, 220, 100); sample.Save(sourcePath, ImageFormat.Png);
        }
        var reference = IconStore.Import(sourcePath, fixtureDirectory);
        if (IconStore.Import(sourcePath, fixtureDirectory) != reference) throw new IOException("Icon import did not reuse identical content.");
        using (var decoded = IconStore.Load(reference, fixtureDirectory))
        {
            if (decoded.Width != 300 || decoded.Height != 180 || decoded.GetPixel(0, 0).A != 0) throw new IOException("Icon import changed dimensions/transparency.");
        }
        foreach (var icon in IconReferences.BuiltIns)
        {
            using var decoded = IconStore.Load(icon.Id, fixtureDirectory);
            if (decoded.Width != 128 || decoded.Height != 128) throw new IOException("Invalid builtin icon.");
        }
        foreach (var index in new[] { 0, 13 })
        {
            var rendered = ProfileImages.Render(new KeyConfig { Index = index, Label = "Custom icon", Icon = reference }, fixtureDirectory);
            if (IconReferences.InspectPng(rendered) != (196, 196)) throw new IOException("Custom device icon dimensions changed.");
        }
        File.WriteAllText(sourcePath, "invalid PNG");
        try { IconStore.Import(sourcePath, fixtureDirectory); throw new IOException("Malformed icon accepted."); }
        catch (ArgumentException) { } catch (IOException error) when (error.Message.StartsWith("Choose a PNG")) { }
        try { IconStore.Load("icons/missing.png", fixtureDirectory); throw new IOException("Missing icon silently substituted."); }
        catch (FileNotFoundException) { }
        try { IconStore.Import(@"\\server\share\image.png", fixtureDirectory); throw new IOException("Network image imported."); }
        catch (IOException error) when (error.Message.StartsWith("Choose a local PNG")) { }
        var withIcons = Profiles.Parse(File.ReadAllText(Path.Combine(fixtureDirectory, "profile.json")));
        withIcons.Keys[0].Icon = reference; withIcons.Keys[13].Icon = "builtin:usage";
        var bundle = ProfileImages.Bundle(withIcons, fixtureDirectory);
        if (!Protocol.CleanBoundaries(bundle)) throw new IOException("Icon page framing failed.");
        var originalNames = ImageNames(bundle);
        withIcons.Keys[0].Label = "Updated image";
        var updatedNames = ImageNames(ProfileImages.Bundle(withIcons, fixtureDirectory));
        if (originalNames["0_0"] == updatedNames["0_0"] || originalNames["1_0"] != updatedNames["1_0"])
            throw new IOException("Device image names do not follow changed/unchanged pixels.");
        if (updatedNames.Values.Any(name => !name.StartsWith("icons/key-", StringComparison.Ordinal) || !name.EndsWith(".png", StringComparison.Ordinal)))
            throw new IOException("Invalid device image asset name.");
        File.WriteAllText(Path.Combine(fixtureDirectory, "profile.json"), System.Text.Json.JsonSerializer.Serialize(withIcons, Profiles.JsonOptions));
    }

    static Dictionary<string, string> ImageNames(byte[] bundle)
    {
        using var archive = new System.IO.Compression.ZipArchive(new MemoryStream(bundle));
        using var manifest = archive.GetEntry("manifest.json")!.Open();
        using var json = System.Text.Json.JsonDocument.Parse(manifest);
        var names = new Dictionary<string, string>();
        foreach (var cell in json.RootElement.EnumerateObject())
        {
            var name = cell.Value.GetProperty("ViewParam")[0].GetProperty("Icon").GetString()!;
            if (archive.GetEntry(name) is null) throw new IOException("Manifest references a missing image.");
            names.Add(cell.Name, name);
        }
        return names;
    }
}
