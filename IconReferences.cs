using System.Buffers.Binary;
using System.Text.RegularExpressions;

namespace D200xDirect;

public sealed record IconDescriptor(string Id, string Title);
public sealed record KeyAppearance(string? Icon);

public static class IconReferences
{
    public const int MaximumPngBytes = 2 * 1024 * 1024;
    public const int MaximumDimension = 1024;
    public static IReadOnlyList<IconDescriptor> BuiltIns { get; } = Array.AsReadOnly(new[]
    {
        new IconDescriptor("builtin:screenshot", "Screenshot"), new IconDescriptor("builtin:record", "Recording"),
        new IconDescriptor("builtin:microphone", "Microphone"), new IconDescriptor("builtin:application", "Application"),
        new IconDescriptor("builtin:gamepad", "Gamepad"), new IconDescriptor("builtin:website", "Website"),
        new IconDescriptor("builtin:market", "Market chart"), new IconDescriptor("builtin:temperature", "Temperature"),
        new IconDescriptor("builtin:usage", "System usage"), new IconDescriptor("builtin:horn", "Horn")
    });

    public static void Validate(string? reference)
    {
        if (reference is null) return;
        if (BuiltIns.Any(icon => icon.Id == reference)) return;
        if (!Regex.IsMatch(reference, @"\Aicons/[A-Za-z0-9][A-Za-z0-9_-]{0,95}\.png\z"))
            throw new ArgumentException("Icon must be a supported builtin: name or icons/filename.png inside your profile folder.");
    }

    // Bound dimensions before asking the native image decoder to allocate pixels.
    public static (int Width, int Height) InspectPng(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is < 33 or > MaximumPngBytes || !bytes[..8].SequenceEqual(new byte[] { 137,80,78,71,13,10,26,10 })
            || BinaryPrimitives.ReadInt32BigEndian(bytes[8..12]) != 13 || !bytes[12..16].SequenceEqual("IHDR"u8))
            throw new ArgumentException("Choose a PNG image up to 2 MB.");
        var width = BinaryPrimitives.ReadInt32BigEndian(bytes[16..20]);
        var height = BinaryPrimitives.ReadInt32BigEndian(bytes[20..24]);
        if (width is < 1 or > MaximumDimension || height is < 1 or > MaximumDimension)
            throw new ArgumentException("PNG dimensions must be between 1 and 1024 pixels on each side.");
        return (width, height);
    }
}
