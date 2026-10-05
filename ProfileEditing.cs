using System.Text.Json;

namespace D200xDirect;

public static class ProfileEditing
{
    // Return a validated copy. A rejected edit must never mutate live mappings.
    public static DeckProfile Update(DeckProfile original, int index, string gesture,
        string label, string background, DeckAction action, KeyAppearance? appearance = null)
    {
        var copy = Profiles.Parse(JsonSerializer.Serialize(original, Profiles.JsonOptions));
        var copiedAction = JsonSerializer.Deserialize<DeckAction>(JsonSerializer.Serialize(action, Profiles.JsonOptions), Profiles.JsonOptions)!;
        if (index is >= 0 and <= 13 && gesture == "press")
        {
            var key = copy.Keys.FirstOrDefault(k => k.Index == index);
            if (key is null) { key = new KeyConfig { Index = index }; copy.Keys.Add(key); }
            key.Label = label; key.Background = background; key.Action = copiedAction;
            if (appearance is not null) key.Icon = appearance.Icon;
        }
        else if (index is 15 or 16 && gesture == "press")
        {
            var side = copy.SideButtons.FirstOrDefault(s => s.Index == index);
            if (side is null) { side = new SideConfig { Index = index }; copy.SideButtons.Add(side); }
            side.Action = copiedAction;
        }
        else if (index is >= 17 and <= 19 && gesture is "left" or "right" or "press")
        {
            var dial = copy.Dials.FirstOrDefault(d => d.Index == index);
            if (dial is null) { dial = new DialConfig { Index = index }; copy.Dials.Add(dial); }
            dial.Label = label;
            switch (gesture) { case "left": dial.Left = copiedAction; break; case "right": dial.Right = copiedAction; break; case "press": dial.Press = copiedAction; break; }
        }
        else throw new ArgumentException("Choose a physical control and supported gesture.");
        Profiles.Validate(copy);
        return copy;
    }
}
