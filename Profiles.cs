using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace D200xDirect;

public sealed class DeckProfile
{
    public int SchemaVersion { get; set; } = 1;
    public string Name { get; set; } = "My D200X";
    public List<KeyConfig> Keys { get; set; } = [];
    public List<DialConfig> Dials { get; set; } = [];
    public List<SideConfig> SideButtons { get; set; } = [];
}

public sealed class KeyConfig
{
    public int Index { get; set; }
    public string Label { get; set; } = "";
    public string Background { get; set; } = "#102038";
    public DeckAction Action { get; set; } = new();
}

public sealed class DialConfig
{
    public int Index { get; set; } = 17;
    public string Label { get; set; } = "";
    public DeckAction Left { get; set; } = new();
    public DeckAction Right { get; set; } = new();
    public DeckAction Press { get; set; } = new();
}

public sealed class SideConfig
{
    public int Index { get; set; } = 15;
    public DeckAction Action { get; set; } = new();
}

public sealed class DeckAction
{
    public string Type { get; set; } = "none";
    public List<string> Keys { get; set; } = [];
    public string? Url { get; set; }
    public string? Path { get; set; }
}

public static class Profiles
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        MaxDepth = 16
    };

    // Named Windows virtual keys; LLM profiles cannot supply scripts or command strings.
    public static readonly IReadOnlyDictionary<string, ushort> VirtualKeys = BuildKeys();

    public static DeckProfile Parse(string json)
    {
        if (json.Length > 1_048_576) throw new ArgumentException("Profile exceeds 1 MB.");
        var profile = JsonSerializer.Deserialize<DeckProfile>(json, JsonOptions)
            ?? throw new ArgumentException("Profile must be a JSON object.");
        Validate(profile);
        return profile;
    }

    public static void Validate(DeckProfile profile)
    {
        if (profile.SchemaVersion != 1) throw new ArgumentException("Supported schemaVersion: 1.");
        if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Length > 80)
            throw new ArgumentException("Profile name must contain 1–80 characters.");
        if (profile.Keys is null || profile.Dials is null || profile.SideButtons is null)
            throw new ArgumentException("Profile arrays must not be null.");
        if (profile.Keys.Count > 14 || profile.Dials.Count > 3 || profile.SideButtons.Count > 2)
            throw new ArgumentException("D200X supports 14 LCD keys, 3 dials and 2 side buttons.");
        var indices = new HashSet<int>();
        foreach (var key in profile.Keys)
        {
            if (key is null || key.Index is < 0 or > 13 || !indices.Add(key.Index))
                throw new ArgumentException("LCD key indices must be unique, 0–13.");
            Label(key.Label);
            if (key.Background is null || !Regex.IsMatch(key.Background, "^#[0-9a-fA-F]{6}$"))
                throw new ArgumentException($"Key {key.Index}: background must be #RRGGBB.");
            ValidateAction(key.Action);
        }
        foreach (var dial in profile.Dials)
        {
            if (dial is null || dial.Index is < 17 or > 19 || !indices.Add(dial.Index))
                throw new ArgumentException("Dial indices must be unique, 17–19.");
            Label(dial.Label);
            ValidateAction(dial.Left); ValidateAction(dial.Right); ValidateAction(dial.Press);
        }
        foreach (var side in profile.SideButtons)
        {
            if (side is null || side.Index is not (15 or 16) || !indices.Add(side.Index))
                throw new ArgumentException("Side button indices must be unique, 15 or 16.");
            ValidateAction(side.Action);
        }
    }

    static void Label(string? label)
    {
        if (label is null || label.Length > 64 || label.Any(char.IsControl))
            throw new ArgumentException("Labels must contain at most 64 printable characters.");
    }

    static void ValidateAction(DeckAction? action) => ActionCatalog.Default.Validate(action);

    public static DeckAction? ActionFor(DeckProfile profile, InputEvent input)
    {
        if (input.Kind == "key" && input.Action == "press")
            return profile.Keys.FirstOrDefault(k => k.Index == input.Index)?.Action;
        if (input.Kind == "side" && input.Action == "press")
            return profile.SideButtons.FirstOrDefault(k => k.Index == input.Index)?.Action;
        if (input.Kind == "dial")
        {
            var dial = profile.Dials.FirstOrDefault(d => d.Index == input.Index);
            return input.Action switch { "left" => dial?.Left, "right" => dial?.Right, "press" => dial?.Press, _ => null };
        }
        return null;
    }

    static Dictionary<string, ushort> BuildKeys()
    {
        var keys = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
        {
            ["Ctrl"] = 0x11, ["Alt"] = 0x12, ["Shift"] = 0x10, ["Win"] = 0x5b,
            ["Enter"] = 0x0d, ["Escape"] = 0x1b, ["Space"] = 0x20, ["Tab"] = 0x09,
            ["Backspace"] = 0x08, ["Delete"] = 0x2e, ["Insert"] = 0x2d,
            ["Home"] = 0x24, ["End"] = 0x23, ["PageUp"] = 0x21, ["PageDown"] = 0x22,
            ["Left"] = 0x25, ["Up"] = 0x26, ["Right"] = 0x27, ["Down"] = 0x28,
            ["VolumeMute"] = 0xad, ["VolumeDown"] = 0xae, ["VolumeUp"] = 0xaf,
            ["MediaNext"] = 0xb0, ["MediaPrevious"] = 0xb1, ["MediaStop"] = 0xb2, ["MediaPlayPause"] = 0xb3
        };
        for (var c = 'A'; c <= 'Z'; c++) keys[c.ToString()] = c;
        for (var c = '0'; c <= '9'; c++) keys[c.ToString()] = c;
        for (var i = 1; i <= 24; i++) keys[$"F{i}"] = (ushort)(0x6f + i);
        return keys;
    }
}
