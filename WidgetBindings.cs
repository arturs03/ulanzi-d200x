using System.Globalization;
using D200xDirect.Providers;

namespace D200xDirect;

public sealed record WidgetBinding
{
    public required string ProviderId { get; init; }
    public required string MetricId { get; init; }
    public required string SourceId { get; init; }
    public required string Unit { get; init; }
    public string Label { get; init; } = "";
    public int Precision { get; init; }

    public void Validate()
    {
        if (!ProviderContract.IsId(ProviderId) || !ProviderContract.IsId(MetricId) || !ProviderContract.IsId(SourceId)
            || Unit is not ("percent" or "celsius" or "usd" or "fps") || Precision is < 0 or > 2
            || Label is null || Label.Length > 16 || Label.Any(char.IsControl))
            throw new ArgumentException("Invalid widget binding, unit, label or precision.");
    }

    public string Format(ProviderSample? sample)
    {
        var text = "--";
        if (sample is { Status: "ok", Value: { } value } && sample.Unit == Unit
            && sample.MetricId == MetricId && sample.SourceId == SourceId && double.IsFinite(value))
        {
            var suffix = Unit switch { "percent" => "%", "celsius" => "°C", "usd" => " USD", "fps" => " FPS", _ => "" };
            text = value.ToString("F" + Precision, CultureInfo.InvariantCulture) + suffix;
        }
        return string.IsNullOrEmpty(Label) ? text : Label + " " + text;
    }
}
