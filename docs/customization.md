# Customize D200X Direct with ChatGPT or another LLM

The app stores your editable profile at `%APPDATA%\D200XDirect\profile.json`. Click **Edit profile** to open it. An LLM can change this file; it does not need to change C# or rebuild the app. An LLM with access to your local files can edit it directly. In a normal chat, copy the generated JSON into the file yourself.

Click **Reload file** after saving. Invalid profiles are rejected and the last valid mappings remain active. Reloading never changes the device display: stop listening, then click **Update screens** explicitly. Confirm the actual image on the D200X. Fully exit Studio before using the device from this app.

The app starts stopped, with actions disabled. Click **Start** to log physical input. Only check **Enable actions** when you want your mappings to execute. No app configures Discord keybinds for you; its corresponding hotkey must also be configured in Discord. Hotkeys use the current foreground application and may be rejected by elevated apps.

Keep screen on is a separate experimental app checkbox, not a JSON action. It defaults off. After sending a profile, select it before listening to send one small image-mode/time update every 5 seconds while the session runs. It is intended to test the reported one-minute idle fallback; the user confirmed that the device stays awake in a session; extended duration and possible flicker still need testing. Do not add unrecognized keep-awake fields to profiles.

The dark app also lets you select any physical control and edit its mapping in the inspector. **Save changes** writes the same validated profile, without executing it or uploading display images. Developers can extend the compiled action registry using [the module API](actions-api.md).

## Prompt to give an LLM

Copy this instruction, your current profile and `profiles/profile.schema.json` into your chat:

> Edit my D200X Direct JSON profile to implement the changes below. Keep schemaVersion 1. Return the complete JSON profile with no comments. Preserve controls I did not ask to change. Use only none, hotkey, open-url and launch actions. Do not add scripts, shell commands, arguments, services, drivers or startup settings. Hotkeys have exactly one main key and optional Ctrl, Alt, Shift or Win modifiers. Use unique hardware indices: LCD keys 0–13, side buttons 15–16, dials 17–19. Key 14 does not exist. Labels are at most 64 printable characters and backgrounds use #RRGGBB. Ask for an application's local .exe path when needed; do not invent it. Here is my current profile and the changes I want:

For example: “Make key 0 open my favorite website, label it Web with a blue background. Make dial 17 adjust volume left/right and mute on press. Leave everything else unchanged.”

## Profile format

- `schemaVersion`: 1.
- `name`: a name with 1–80 characters.
- `keys`: up to 14 entries, each with a unique `index` (0–13), `label`, `background` and `action`. Missing keys are blank and inactive.
- Key 13 is the double-width screen. The app selects image mode when sending a page so its firmware clock/gauges do not overlap the configured label. Its image and app preview use the wide aspect ratio.
- `icon` is optional on LCD keys. Use `builtin:screenshot`, `builtin:record`, `builtin:microphone`, `builtin:application`, `builtin:gamepad`, `builtin:website`, `builtin:market`, `builtin:temperature`, `builtin:usage`, or `builtin:horn`. A custom icon uses `icons/filename.png` relative to the profile folder. Import it through the app's PNG button, or provide that folder with a shared profile. Do not invent a file reference, use absolute paths, or add remote image URLs. Omit `icon` for text-only keys. Icons are independent of actions: a horn icon alone does not play a sound.
- `dials`: up to three entries, each with a unique `index` (17–19), `label` and `left`, `right`, `press` actions. Dial labels are shown in the JSON; custom dial-area display is not implemented.
- `sideButtons`: up to two entries, each with a unique `index` (15 or 16) and `action`.

```json
{
  "schemaVersion": 1,
  "name": "My desk",
  "keys": [
    {
      "index": 0,
      "label": "Web",
      "background": "#164063",
      "action": { "type": "open-url", "url": "https://example.com" }
    },
    {
      "index": 1,
      "label": "Play / Pause",
      "background": "#28543A",
      "action": { "type": "hotkey", "keys": ["MediaPlayPause"] }
    }
  ],
  "dials": [
    {
      "index": 17,
      "label": "Volume",
      "left": { "type": "hotkey", "keys": ["VolumeDown"] },
      "right": { "type": "hotkey", "keys": ["VolumeUp"] },
      "press": { "type": "hotkey", "keys": ["VolumeMute"] }
    }
  ],
  "sideButtons": []
}
```

Supported hotkey names: A–Z, 0–9, F1–F24, Ctrl, Alt, Shift, Win, Enter, Escape, Space, Tab, Backspace, Delete, Insert, Home, End, PageUp, PageDown, Left, Up, Right, Down, VolumeMute, VolumeDown, VolumeUp, MediaNext, MediaPrevious, MediaStop, MediaPlayPause. Names are case insensitive in the app; use the schema's spelling when generating JSON.

`launch` accepts `{"type":"launch","path":"C:\\Path\\Application.exe"}`. The .exe must exist locally. There are no command-line arguments, script actions, UNC launches or inline credentials. `open-url` accepts only HTTP/HTTPS links without embedded credentials. Use `{"type":"none"}` for an inactive control. Unknown fields and unsupported action types are rejected.

Profiles are plain text: avoid storing passwords, tokens or personal secrets in them. The app does not connect to an LLM service or transmit profiles anywhere. ChatGPT/LLM customization means editing this documented file format; it requires no paid API integration.

## Current limits

The editor's **Shortcut preset** presets fill a label, icon and shortcut; **Save changes** persists them. Screenshot region uses Win+Shift+S; recording region uses Win+Shift+R to open Snipping Tool, where you choose a region and start recording. Game Bar app recording uses Win+Alt+R as a separate preset. These are Windows shortcuts, not an embedded capture engine. They depend on the installed Windows tool and its settings. Actual shortcut delivery needs physical testing.

**Import profile** can import a local JSON file, copy validated custom PNGs into the profile's icons folder, and back up the previous profile as `profile.previous.json`. It disables Enable actions and does not upload a page. Stop and explicitly Update screens to display the imported page. PNGs must be at most 2 MB and 1024 pixels per side; imports are normalized to at most 512 pixels.

This preview supports one active profile at a time, built-in/custom PNG key icons and profile import. Dynamic sensors, multiple device pages, automatic reconnect, custom dial-area images and automatic startup are not implemented. Physical actions, icon display and lifecycle still require validation on the D200X; automated checks alone do not establish device operation.
