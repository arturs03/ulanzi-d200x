namespace D200xDirect.Providers;

public sealed record InstalledProvider(string Directory, ProviderManifest Manifest);
public sealed record ProviderDiscoveryResult(IReadOnlyList<InstalledProvider> Providers, IReadOnlyList<string> Problems);

public static class ProviderDiscovery
{
    public static ProviderDiscoveryResult Scan(string directory)
    {
        var found = new List<InstalledProvider>();
        var problems = new List<string>();
        if (!System.IO.Directory.Exists(directory)) return new(found, problems);
        try
        {
            RequireLocalPath(directory);
            var folders = System.IO.Directory.EnumerateDirectories(directory).Take(65).ToArray();
            if (folders.Length > 64) throw new IOException("At most 64 provider folders are supported.");
            foreach (var folder in folders)
            {
                try { found.Add(Load(folder)); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException)
                { problems.Add($"Provider folder rejected ({error.GetType().Name})."); }
            }
            var duplicates = found.GroupBy(x => x.Manifest.Id, StringComparer.Ordinal).Where(x => x.Count() > 1).Select(x => x.Key).ToHashSet();
            foreach (var duplicate in duplicates) problems.Add($"Duplicate provider ID: {duplicate}.");
            found.RemoveAll(x => duplicates.Contains(x.Manifest.Id));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        { found.Clear(); problems.Add($"Provider directory rejected ({error.GetType().Name})."); }
        return new(found.AsReadOnly(), problems.AsReadOnly());
    }

    public static InstalledProvider Load(string directory)
    {
        var root = Path.GetFullPath(directory);
        RequireLocalPath(root);
        var path = Path.Combine(root, "plugin.json");
        RequireLocalPath(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is 0 or > ProviderContract.MaximumBytes) throw new IOException("Invalid provider manifest size.");
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        var manifest = ProviderContract.Parse<ProviderManifest>(bytes);
        manifest.Validate();
        var executable = Path.Combine(root, manifest.Executable);
        RequireLocalPath(executable);
        if (!File.Exists(executable)) throw new IOException("Provider executable is missing.");
        return new(root, manifest);
    }

    public static void RequireLocalPath(string path)
    {
        var full = Path.GetFullPath(path);
        if (!Path.IsPathFullyQualified(full) || full.StartsWith(@"\\", StringComparison.Ordinal))
            throw new IOException("Providers must use local paths.");
        for (var current = full; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked provider paths are unsupported.");
    }
}
