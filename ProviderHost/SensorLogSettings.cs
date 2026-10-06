using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace D200xDirect.Providers;

// Machine-local settings, deliberately separate from portable key bindings.
public sealed record SensorLogSettings
{
    public required string Directory { get; init; }
    public required string TimeZoneId { get; init; }
    public string? CpuTemperature { get; init; }
    public string? GpuTemperature { get; init; }
    public string? GpuHotspot { get; init; }
    public string? GpuUsage { get; init; }

    public void Validate()
    {
        if (Directory is not { Length: > 2 and <= 1024 } || !Regex.IsMatch(Directory, @"^[A-Za-z]:[\\/]", RegexOptions.CultureInvariant)
            || Directory.Any(char.IsControl) || Directory.IndexOf(':', 2) >= 0 || Path.GetFullPath(Directory) != Directory)
            throw new ArgumentException("Choose a local absolute log folder.");
        SensorLogMetadata.CheckLocalDrive(Directory);
        if (TimeZoneId != TimeZoneInfo.Local.Id) throw new ArgumentException("These logs must use this computer's Windows time zone. Select the source again after changing the time zone.");
        if (CpuTemperature is null && GpuTemperature is null && GpuHotspot is null && GpuUsage is null) throw new ArgumentException("Select at least one hardware sensor.");
        foreach (var id in new[] { CpuTemperature, GpuTemperature, GpuHotspot }.OfType<string>())
            if (!Regex.IsMatch(id, @"^/(amdcpu|intelcpu|nvidiagpu|atigpu)/[0-9]{1,3}/temperature/[0-9]{1,3}$", RegexOptions.CultureInvariant))
                throw new ArgumentException("Select an exact supported temperature sensor.");
        if (GpuUsage is { } load && !Regex.IsMatch(load, @"^/(nvidiagpu|atigpu)/[0-9]{1,3}/load/[0-9]{1,3}$", RegexOptions.CultureInvariant))
            throw new ArgumentException("Select an exact GPU Core load sensor.");
        if (CpuTemperature is { } cpu && !cpu.StartsWith("/amdcpu/", StringComparison.Ordinal) && !cpu.StartsWith("/intelcpu/", StringComparison.Ordinal))
            throw new ArgumentException("CPU temperature requires a CPU package sensor.");
        foreach (var gpu in new[] { GpuTemperature, GpuHotspot }.OfType<string>())
            if (!gpu.StartsWith("/nvidiagpu/", StringComparison.Ordinal) && !gpu.StartsWith("/atigpu/", StringComparison.Ordinal))
                throw new ArgumentException("GPU temperatures require a GPU sensor.");
        if (GpuTemperature is { } core && GpuHotspot is { } hotspot && (core == hotspot || Hardware(core) != Hardware(hotspot)))
            throw new ArgumentException("GPU core and hotspot must be different sensors on the same GPU.");
        if (new[] { GpuTemperature, GpuHotspot, GpuUsage }.OfType<string>().Select(Hardware).Distinct(StringComparer.Ordinal).Count() > 1)
            throw new ArgumentException("GPU temperature, hotspot and load must belong to the same GPU.");
    }

    static string Hardware(string id) => string.Join('/', id.Split('/').Take(3));

    public static SensorLogSettings? Load(string path)
    {
        if (!File.Exists(path)) return null;
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length is 0 or > 8192) throw new IOException("Invalid sensor settings size.");
        var bytes = new byte[(int)file.Length]; file.ReadExactly(bytes);
        var settings = ProviderContract.Parse<SensorLogSettings>(bytes); settings.Validate(); return settings;
    }

    public void Save(string path)
    {
        Validate();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(this, ProviderContract.Json);
        if (bytes.Length > 8192) throw new IOException("Sensor settings exceed limits.");
        var temporary = path + ".new";
        try { File.WriteAllBytes(temporary, bytes); File.Move(temporary, path, overwrite: true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

public sealed record SensorColumn(string Id, string Name)
{
    public override string ToString() => $"{Name} · {Id}";
}

public static class SensorLogMapping
{
    public static SensorLogSettings WithGpuUsage(SensorLogSettings current, IReadOnlyList<SensorColumn> columns)
    {
        if (current.GpuUsage is not null) return current;
        var configuredGpu = current.GpuTemperature ?? current.GpuHotspot;
        var hardware = configuredGpu is null ? null : string.Join('/', configuredGpu.Split('/').Take(3));
        var matches = columns.Where(c => Compatible(c, 3) && (hardware is null || string.Join('/', c.Id.Split('/').Take(3)) == hardware)).ToArray();
        if (matches.Length != 1) return current;
        var next = current with { GpuUsage = matches[0].Id }; next.Validate(); return next;
    }

    public static bool Compatible(SensorColumn column, int role)
    {
        if (!Regex.IsMatch(column.Id, @"^/(amdcpu|intelcpu|nvidiagpu|atigpu)/[0-9]{1,3}/(temperature|load)/[0-9]{1,3}$", RegexOptions.CultureInvariant)) return false;
        var cpu = column.Id.StartsWith("/amdcpu/", StringComparison.Ordinal) || column.Id.StartsWith("/intelcpu/", StringComparison.Ordinal);
        if (role == 3) return !cpu && column.Id.Contains("/load/", StringComparison.Ordinal) && column.Name.Equals("GPU Core", StringComparison.OrdinalIgnoreCase);
        if (!column.Id.Contains("/temperature/", StringComparison.Ordinal)) return false;
        return role switch
        {
            0 => cpu && column.Name.Equals("CPU Package", StringComparison.OrdinalIgnoreCase),
            1 => !cpu && column.Name.Equals("GPU Core", StringComparison.OrdinalIgnoreCase),
            2 => !cpu && column.Name.Equals("GPU Hot Spot", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    // Suggest only exact, unique roles. Multiple GPUs require a user choice.
    public static string?[] Suggest(IReadOnlyList<SensorColumn> columns)
    {
        var result = new string?[4];
        var cpu = columns.Where(c => Compatible(c, 0)).ToArray();
        if (cpu.Length == 1) result[0] = cpu[0].Id;
        var gpu = columns.Where(c => Compatible(c, 1) || Compatible(c, 2) || Compatible(c, 3)).ToArray();
        if (gpu.Select(c => string.Join('/', c.Id.Split('/').Take(3))).Distinct(StringComparer.Ordinal).Count() == 1)
            for (var role = 1; role < 4; role++)
            {
                var matches = gpu.Where(c => Compatible(c, role)).ToArray();
                if (matches.Length == 1) result[role] = matches[0].Id;
            }
        return result;
    }
}

public static class SensorLogMetadata
{
    public static void CheckLocalDrive(string path)
    {
        var type = new DriveInfo(Path.GetPathRoot(path) ?? throw new ArgumentException("Choose a local log folder.")).DriveType;
        if (type is not (DriveType.Fixed or DriveType.Removable or DriveType.Ram)) throw new ArgumentException("Choose a local drive, not a mapped network folder.");
    }

    public static void CheckLocalPath(string path)
    {
        CheckLocalDrive(path);
        for (var current = new DirectoryInfo(path); current is not null; current = current.Parent)
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Choose a local folder without directory links.");
    }

    // Only header metadata is read. This never starts a monitor/provider or treats a value as live.
    public static async Task<SensorColumn[]> ReadAsync(string folder, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!Path.IsPathFullyQualified(folder) || !Regex.IsMatch(folder, @"^[A-Za-z]:\\", RegexOptions.CultureInvariant)) throw new ArgumentException("Choose a local log folder.");
        CheckLocalPath(folder);
        // Prefer today's header. A historical header can be used for explicit assignment while stopped.
        var today = Path.Combine(folder, $"OpenHardwareMonitorLog-{DateTime.Now:yyyy-MM-dd}.csv");
        string filename;
        if (File.Exists(today)) filename = today;
        else
        {
            var candidates = System.IO.Directory.EnumerateFiles(folder, "OpenHardwareMonitorLog-????-??-??.csv").Take(368).ToArray();
            if (candidates.Length == 368) throw new IOException("Too many historical logs to inspect. Select a folder with today's log.");
            filename = candidates.OrderByDescending(Path.GetFileName, StringComparer.Ordinal).FirstOrDefault() ?? throw new IOException("No Open Hardware Monitor CSV headers found.");
        }
        if ((File.GetAttributes(filename) & FileAttributes.ReparsePoint) != 0) throw new IOException("Sensor log cannot be a file link.");
        token.ThrowIfCancellationRequested();
        await using var file = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, useAsync: true);
        var buffer = new byte[65536]; var length = await file.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken: token).ConfigureAwait(false); var second = -1; var first = -1;
        for (var i = 0; i < length; i++) if (buffer[i] == 10) { if (first < 0) first = i; else { second = i; break; } }
        if (second < 0) throw new IOException("CSV headers missing or exceed 64 KiB.");
        return ParseHeaders(ProviderContract.Utf8.GetString(buffer, 0, second));
    }

    public static SensorColumn[] ParseHeaders(string text)
    {
        var rows = text.TrimStart('\uFEFF').Split('\n');
        if (rows.Length != 2 || text.Length > 65536) throw new IOException("Invalid CSV headers.");
        var ids = ParseRow(rows[0].TrimEnd('\r')); var names = ParseRow(rows[1].TrimEnd('\r'));
        if (ids.Length < 2 || ids.Length > 1025 || ids.Length != names.Length || ids[0] != "" || names[0] != "Time"
            || ids.Skip(1).Any(id => id.Length is 0 or > 256) || ids.Skip(1).Distinct(StringComparer.Ordinal).Count() != ids.Length - 1
            || names.Any(name => name.Length > 256 || name.Any(char.IsControl))) throw new IOException("Invalid or ambiguous CSV headers.");
        return ids.Skip(1).Select((id, index) => new SensorColumn(id, names[index + 1])).ToArray();
    }

    static string[] ParseRow(string line)
    {
        var fields = new List<string>(); var value = new StringBuilder(); var quoted = false; var closed = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"') { if (i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i++; } else { quoted = false; closed = true; } }
                else value.Append(c);
            }
            else if (c == ',') { fields.Add(value.ToString()); value.Clear(); closed = false; }
            else if (c == '"' && value.Length == 0 && !closed) quoted = true;
            else if (c == '"' || closed) throw new IOException("Malformed CSV quoting.");
            else value.Append(c);
        }
        if (quoted) throw new IOException("Incomplete CSV quote.");
        fields.Add(value.ToString()); return fields.ToArray();
    }
}
