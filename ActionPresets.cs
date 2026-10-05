namespace D200xDirect;

public sealed record ActionPreset(string Title, string Label, string Icon, IReadOnlyList<string> Keys)
{
    public DeckAction CreateAction() => new() { Type = "hotkey", Keys = Keys.ToList() };
}

public static class ActionPresets
{
    public static IReadOnlyList<ActionPreset> All { get; } = Array.AsReadOnly(new[]
    {
        Preset("Screenshot region", "Screenshot", "screenshot", "Win", "Shift", "S"),
        Preset("Record region (Snipping Tool)", "Record", "record", "Win", "Shift", "R"),
        Preset("Toggle app recording (Game Bar)", "Record app", "record", "Win", "Alt", "R"),
        Preset("Discord mute (Ctrl+Alt+Shift+M)", "Mute", "microphone", "Ctrl", "Alt", "Shift", "M"),
        Preset("Play / Pause", "Play / Pause", "application", "MediaPlayPause"),
        Preset("Volume up", "Volume +", "usage", "VolumeUp"),
        Preset("Volume down", "Volume −", "usage", "VolumeDown"),
        Preset("Volume mute", "Mute audio", "microphone", "VolumeMute")
    });
    static ActionPreset Preset(string title, string label, string icon, params string[] keys)
        => new(title, label, "builtin:" + icon, Array.AsReadOnly(keys));
}
