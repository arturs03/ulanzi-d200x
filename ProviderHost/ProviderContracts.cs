using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace D200xDirect.Providers;

public static class ProviderContract
{
    public const int Version = 1;
    public const int MaximumBytes = 65_536;
    public const int MaximumMetrics = 64;
    public static readonly TimeSpan RequestDeadline = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan ShutdownDeadline = TimeSpan.FromSeconds(2);
    public static readonly UTF8Encoding Utf8 = new(false, true);
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        MaxDepth = 16
    };

    public static bool IsId(string? value) => value is { Length: > 0 and <= 96 }
        && Regex.IsMatch(value, "^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", RegexOptions.CultureInvariant);

    public static T Parse<T>(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is 0 or > MaximumBytes) throw new IOException("Invalid provider message size.");
        // Strict decoding and duplicate-member rejection keep C# and Rust interpretations aligned.
        var text = Utf8.GetString(bytes);
        using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 16 });
        CheckMembers(document.RootElement);
        return JsonSerializer.Deserialize<T>(text, Json) ?? throw new IOException("Provider object is null.");
    }

    static void CheckMembers(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new IOException("Duplicate provider JSON member.");
                CheckMembers(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) CheckMembers(item);
    }
}

public sealed record ProviderMetric
{
    public required string Id { get; init; }
    public required string Unit { get; init; }
    public required int MinimumIntervalMs { get; init; }
    public required string[] SourceIds { get; init; }
}

public sealed record ProviderManifest
{
    public required int ManifestVersion { get; init; }
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required int ProtocolVersion { get; init; }
    public required string Executable { get; init; }
    public required string Platform { get; init; }
    public required string[] Capabilities { get; init; }
    public required ProviderMetric[] Metrics { get; init; }

    public void Validate()
    {
        if (ManifestVersion != 1 || ProtocolVersion != ProviderContract.Version || Platform != "windows-x64")
            throw new IOException("Unsupported provider manifest, protocol or platform.");
        if (!ProviderContract.IsId(Id) || Name is not { Length: > 0 and <= 80 } || string.IsNullOrWhiteSpace(Name) || Name.Any(char.IsControl)
            || Version is null || Version.Length > 64 || !Regex.IsMatch(Version, "^[0-9]+\\.[0-9]+\\.[0-9]+(?:-[a-zA-Z0-9.]+)?$"))
            throw new IOException("Invalid provider identity.");
        if (Executable is null || !Regex.IsMatch(Executable, "^[A-Za-z0-9][A-Za-z0-9_-]{0,95}\\.exe$"))
            throw new IOException("Provider executable must be a local .exe filename.");
        if (Capabilities is null || Capabilities.Length > 16 || Capabilities.Distinct().Count() != Capabilities.Length
            || Capabilities.Any(x => !ProviderContract.IsId(x))) throw new IOException("Invalid provider capabilities.");
        if (Metrics is null || Metrics.Length is 0 or > ProviderContract.MaximumMetrics
            || Metrics.Any(x => x is null) || Metrics.Select(x => x.Id).Distinct().Count() != Metrics.Length)
            throw new IOException("Invalid or duplicate provider metrics.");
        foreach (var metric in Metrics)
        {
            if (!ProviderContract.IsId(metric.Id) || metric.Unit is not ("percent" or "celsius" or "usd" or "fps")
                || metric.MinimumIntervalMs is < 1000 or > 86_400_000 || metric.SourceIds is null
                || metric.SourceIds.Length is 0 or > 64 || metric.SourceIds.Any(x => !ProviderContract.IsId(x))
                || metric.SourceIds.Distinct().Count() != metric.SourceIds.Length)
                throw new IOException("Invalid provider metric definition.");
        }
    }
}

public sealed record MetricSelection(string MetricId, string SourceId);

public sealed record ProviderSample
{
    public required string MetricId { get; init; }
    public required string SourceId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required double? Value { get; init; }
    public required string Unit { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required DateTimeOffset? ObservedAt { get; init; }
    public required string Status { get; init; }
    public string? Code { get; init; }
}

public sealed record ProviderMessage
{
    public required int ProtocolVersion { get; init; }
    public required int RequestId { get; init; }
    public required string Type { get; init; }
    public string? ProviderId { get; init; }
    public MetricSelection[]? Selections { get; init; }
    public ProviderSample[]? Samples { get; init; }
    public SensorLogSettings? SensorLog { get; init; }
}

public enum ProviderState { Starting, Running, Stopping, Stopped, Failed }

// Fixed host text only: exception messages may contain private paths or untrusted provider JSON.
public static class ProviderDiagnostics
{
    public static string? MonitorStatus(IReadOnlyList<ProviderSample> samples)
    {
        var selected = samples.Where(s => s.SourceId == "monitor.local").ToArray();
        if (selected.Length == 0) return null;
        var missing = selected.Where(s => s.Status != "ok").ToArray();
        if (missing.Length == 0) return "Hardware monitor readings are available. Update screens sends their current values.";
        var reasons = missing.Select(s => s.Code switch
        {
            "source-not-configured" => "select Sensor source…",
            "source-unavailable" => "no readable log for today; run your monitor with CSV logging enabled",
            "stale-source" => "log readings are stale; check that monitor logging updates at least once every five seconds",
            "sensor-unavailable" => "a selected sensor is missing or invalid; review Sensor source…",
            "time-zone-changed" => "Windows time zone changed; select Sensor source… again",
            _ => "log data is invalid or unavailable; check the source and format"
        }).Distinct(StringComparer.Ordinal);
        return "Hardware monitor data unavailable: " + string.Join("; ", reasons) + ". Missing values show --.";
    }

    public static string Describe(Exception error) => error switch
    {
        TimeoutException => "response timed out",
        System.Threading.Channels.ChannelClosedException { InnerException: { } inner } => Describe(inner),
        System.Threading.Channels.ChannelClosedException => "provider closed its output",
        JsonException or DecoderFallbackException => "invalid provider response",
        System.ComponentModel.Win32Exception => "Windows could not start or manage the provider",
        UnauthorizedAccessException => "access denied",
        ArgumentException => "invalid provider or source settings",
        OperationCanceledException => "provider stopped",
        InvalidOperationException => "provider request state is invalid",
        IOException => "provider I/O or response validation failed",
        _ => "unexpected provider failure"
    };
}

// Receipt time is never used to make an old source observation fresh.
public sealed class ProviderValues
{
    readonly object gate = new();
    sealed record StoredSample(ProviderSample Sample, long ReceivedAt, TimeSpan AgeAtReceipt);
    readonly Dictionary<MetricSelection, StoredSample> samples = [];
    public void Replace(IEnumerable<ProviderSample> values)
    {
        lock (gate)
        {
            var next = new Dictionary<MetricSelection, StoredSample>();
            var now = DateTimeOffset.UtcNow;
            foreach (var sample in values)
            {
                var key = new MetricSelection(sample.MetricId, sample.SourceId);
                var stored = samples.TryGetValue(key, out var old) && old.Sample.ObservedAt == sample.ObservedAt
                    ? old with { Sample = sample }
                    : new StoredSample(sample, System.Diagnostics.Stopwatch.GetTimestamp(), sample.ObservedAt is { } time && now > time ? now - time : TimeSpan.Zero);
                next.Add(key, stored);
            }
            samples.Clear();
            foreach (var pair in next) samples.Add(pair.Key, pair.Value);
        }
    }
    public void Clear() { lock (gate) samples.Clear(); }
    public ProviderSample? Get(MetricSelection selection, DateTimeOffset now, TimeSpan maximumAge)
    {
        lock (gate)
        {
            if (!samples.TryGetValue(selection, out var stored) || stored.Sample.Status != "ok" || stored.Sample.ObservedAt is not { } observed
                || now - observed > maximumAge || observed - now > TimeSpan.FromSeconds(2)
                || stored.AgeAtReceipt + System.Diagnostics.Stopwatch.GetElapsedTime(stored.ReceivedAt) > maximumAge) return null;
            return stored.Sample;
        }
    }
}
