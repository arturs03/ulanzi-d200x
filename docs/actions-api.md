# Action modules

Preview 4 introduces a local C# module API. It is the foundation for expanding integrations; it is not an HTTP service or a loader for downloaded plugins. End users configure actions through the editor or validated JSON. Adding a new action implementation requires rebuilding the app.

## Current architecture

```text
Physical key / dial / side event
    → press deduplication and Run actions gate
    → Profiles.ActionFor(profile, event)
    → ActionCatalog: validate, resolve registered action
    → IActionModule.ExecuteAsync(action, context, cancellation)
    → IActionPlatform: Windows shortcut, URL or local application
```

The editor reads ActionCatalog.Descriptors. Profile validation and execution use the same catalog, so a handler cannot be exposed in the UI while missing from validation. Unknown action IDs are rejected. Registration rejects duplicates. Profiles stay at schemaVersion 1 with existing `none`, `hotkey`, `open-url` and `launch` IDs.

| Contract | Purpose |
| --- | --- |
| ActionDescriptor | Stable ID, UI title, description and input kind |
| IActionModule.Validate | Reject malformed or unsupported configuration before side effects |
| IActionModule.ExecuteAsync | Execute one action with cancellation and event context |
| ActionContext | Physical source event and injected platform capabilities |
| IActionPlatform | Existing Windows operations; replace with a fake in tests |
| ActionCatalog | Explicit registrations, lookup, metadata and shared validation/dispatch |
| ProfileEditing.Update | Produce a validated profile copy without mutating live mappings |

The desktop adapter is `Desktop/ActionRunner.cs`; contracts and built-in modules are in `Actions.cs`. `Profiles.cs` supplies hotkey names, profile validation and event selection. `profiles/profile.schema.json` documents the public JSON contract.

## Use the API

```csharp
// platform is your Windows adapter or a test fake. This does not open a HID handle.
var action = new DeckAction { Type = "hotkey", Keys = ["VolumeUp"] };
var input = new InputEvent(17, "dial", "right");
await ActionCatalog.Default.ExecuteAsync(
    action, new ActionContext(input, platform), cancellationToken);
```

The catalog validates again at execution and rejects pre-cancelled calls before host access. The desktop starts with Run actions off. Profile loading, editing, saving and page uploads never execute mappings. Hotkeys target Windows/current foreground context; application-specific keybinds must match separately.

## Current useful controls

- Audio: VolumeUp, VolumeDown, VolumeMute.
- Media: MediaPlayPause, MediaNext, MediaPrevious, MediaStop.
- Application shortcuts: named keys with optional Ctrl/Alt/Shift/Win.
- Websites: HTTP/HTTPS addresses without credentials.
- Applications: absolute local .exe paths without arguments.
- Dials: independent left/right/press actions. Side buttons: press actions.

These use existing capabilities and do not require an integration server. Discord mute/deafen can use configured Discord shortcuts; specific soundboard triggering still needs investigation. Monitoring/display updates are a separate feature from action execution.

## Adding an integration

1. Define a stable action ID and explicit configuration, permissions, side effects and error behavior. Keep secrets outside profiles; document only a secure reference. Do not use arbitrary command strings as an integration API.
2. Implement an IActionModule and register it explicitly. Extend the host adapter only with the specific capabilities it needs. Update the schema, editor input handling and LLM guide together if new fields or input kinds are needed. The current DeckAction fields are intentionally limited; registration alone does not add arbitrary JSON parameters.
3. Test validation, cancellation, error handling and no side effects before execution using injected fakes. Confirm older profiles still load. Test actual physical action delivery separately.
4. Keep handlers short and nonblocking. The current native actions run directly in the input loop. A slow/network integration needs a bounded worker queue, operation deadlines and stop behavior before it is enabled; do not put unbounded I/O in a handler.

Compiled in-process modules are trusted application code; an interface is not a security sandbox. Untrusted or failure-prone integrations should run in a separate process with a versioned, restricted request/response protocol, bounded messages and deadlines. That host is a future design, not implemented isolation. No module receives the USB writer through this API.

## Expansion priorities to agree

The first practical set can reuse audio/media keys, Discord keybinds and app/website shortcuts. Later candidates include app-specific audio control, OBS through its documented API, profile/page switching and the existing local sensor bridge. Each needs its own reviewed contract and tests. No network API, automatic script execution, custom driver or background service has been added by this module foundation.
