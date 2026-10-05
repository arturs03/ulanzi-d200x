namespace D200xDirect;

public enum ActionValueKind { None, Shortcut, Url, Application }

public sealed record ActionDescriptor(string Id, string Title, string Description, ActionValueKind ValueKind);

// This capability boundary contains no HID handle, shell, sensor or driver access.
public interface IActionPlatform
{
    void SendHotkey(IReadOnlyList<string> keys);
    void OpenUrl(string url);
    void LaunchApplication(string path);
}

public sealed record ActionContext(InputEvent Input, IActionPlatform Platform);

public interface IActionModule
{
    ActionDescriptor Descriptor { get; }
    void Validate(DeckAction action);
    ValueTask ExecuteAsync(DeckAction action, ActionContext context, CancellationToken cancellation);
}

public sealed class ActionCatalog
{
    readonly IReadOnlyDictionary<string, IActionModule> modules;
    public static ActionCatalog Default { get; } = new(BuiltInActions.Create());
    public IReadOnlyList<ActionDescriptor> Descriptors { get; }

    public ActionCatalog(IEnumerable<IActionModule> registrations)
    {
        var table = new Dictionary<string, IActionModule>(StringComparer.Ordinal);
        foreach (var module in registrations)
        {
            if (module is null || string.IsNullOrWhiteSpace(module.Descriptor.Id) || !table.TryAdd(module.Descriptor.Id, module))
                throw new ArgumentException("Action module IDs must be nonempty and unique.");
        }
        modules = table;
        Descriptors = Array.AsReadOnly(table.Values.Select(m => m.Descriptor).ToArray());
    }

    public ActionDescriptor Describe(string id) => Resolve(id).Descriptor;
    IActionModule Resolve(string? id) => id is not null && modules.TryGetValue(id, out var module)
        ? module : throw new ArgumentException($"Unsupported action type: {id ?? "null"}.");

    public void Validate(DeckAction? action)
    {
        if (action is null || action.Keys is null) throw new ArgumentException("Actions must not be null.");
        Resolve(action.Type).Validate(action);
    }

    public ValueTask ExecuteAsync(DeckAction action, ActionContext context, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        Validate(action); // Execution never bypasses validation, including direct API callers.
        return Resolve(action.Type).ExecuteAsync(action, context, cancellation);
    }
}

static class BuiltInActions
{
    public static IEnumerable<IActionModule> Create() =>
    [
        new Module(new("none", "Unassigned", "This control has no action.", ActionValueKind.None)),
        new Module(new("hotkey", "Keyboard shortcut", "A shortcut, media key or volume key. Sent to Windows.", ActionValueKind.Shortcut)),
        new Module(new("open-url", "Open website", "Open an HTTP or HTTPS link in your default browser.", ActionValueKind.Url)),
        new Module(new("launch", "Launch application", "Open a local .exe application, without arguments.", ActionValueKind.Application))
    ];

    sealed class Module(ActionDescriptor descriptor) : IActionModule
    {
        public ActionDescriptor Descriptor { get; } = descriptor;
        public void Validate(DeckAction action)
        {
            if (Descriptor.ValueKind == ActionValueKind.Shortcut)
            {
                if (action.Keys.Count is < 1 or > 5 || action.Keys.Any(k => k is null || !Profiles.VirtualKeys.ContainsKey(k)))
                    throw new ArgumentException("Hotkeys need 1–5 supported key names. See docs/customization.md.");
                if (action.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != action.Keys.Count)
                    throw new ArgumentException("Hotkey keys must not be duplicated.");
                var modifiers = new HashSet<string>(["Ctrl", "Alt", "Shift", "Win"], StringComparer.OrdinalIgnoreCase);
                if (action.Keys.Count(k => !modifiers.Contains(k)) != 1)
                    throw new ArgumentException("Hotkeys need exactly one main key, plus optional modifiers.");
            }
            else if (action.Keys.Count != 0) throw new ArgumentException("keys only applies to hotkey actions.");
            if (Descriptor.ValueKind == ActionValueKind.Url)
            {
                if (!Uri.TryCreate(action.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo))
                    throw new ArgumentException("URLs must be absolute HTTP/HTTPS addresses without credentials.");
            }
            else if (action.Url is not null) throw new ArgumentException("url only applies to open-url actions.");
            if (Descriptor.ValueKind == ActionValueKind.Application)
            {
                if (string.IsNullOrWhiteSpace(action.Path) || !Path.IsPathFullyQualified(action.Path)
                    || action.Path.StartsWith(@"\\") || !action.Path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || action.Path.Contains('"'))
                    throw new ArgumentException("launch requires an absolute local .exe path, without arguments.");
            }
            else if (action.Path is not null) throw new ArgumentException("path only applies to launch actions.");
        }

        public ValueTask ExecuteAsync(DeckAction action, ActionContext context, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            switch (Descriptor.ValueKind)
            {
                case ActionValueKind.Shortcut: context.Platform.SendHotkey(action.Keys); break;
                case ActionValueKind.Url: context.Platform.OpenUrl(action.Url!); break;
                case ActionValueKind.Application: context.Platform.LaunchApplication(action.Path!); break;
            }
            return ValueTask.CompletedTask;
        }
    }
}
