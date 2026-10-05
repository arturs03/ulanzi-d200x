# Customize D200X Direct with ChatGPT or another LLM

The app stores your editable profile at `%APPDATA%\D200XDirect\profile.json`. Click **Edit profile JSON** to open it. An LLM can change this file; it does not need to change C# or rebuild the app. An LLM with access to your local files can edit it directly. In a normal chat, copy the generated JSON into the file yourself.

Click **Reload profile** after saving. Invalid profiles are rejected and the last valid mappings remain active. Reloading never changes the device display: stop listening, then click **Send profile to device** explicitly. Confirm the actual image on the D200X. Fully exit Studio before using the device from this app.

The app starts stopped, with actions disabled. Click **Start listening** to log physical input. Only check **Enable configured actions** when you want your mappings to execute. No app configures Discord keybinds for you; its corresponding hotkey must also be configured in Discord. Hotkeys use the current foreground application and may be rejected by elevated apps.

Keep device awake is a separate experimental app checkbox, not a JSON action. It defaults off. After sending a profile, select it before listening to send one small image-mode/time update every 5 seconds while the session runs. It is intended to test the reported one-minute idle fallback; physical behavior and possible flicker still need verification. Do not add unrecognized keep-awake fields to profiles.

## Prompt to give an LLM

Copy this instruction, your current profile and `profiles/profile.schema.json` into your chat:

> Edit my D200X Direct JSON profile to implement the changes below. Keep schemaVersion 1. Return the complete JSON profile with no comments. Preserve controls I did not ask to change. Use only none, hotkey, open-url and launch actions. Do not add scripts, shell commands, arguments, services, drivers or startup settings. Hotkeys have exactly one main key and optional Ctrl, Alt, Shift or Win modifiers. Use unique hardware indices: LCD keys 0–13, side buttons 15–16, dials 17–19. Key 14 does not exist. Labels are at most 64 printable characters and backgrounds use #RRGGBB. Ask for an application's local .exe path when needed; do not invent it. Here is my current profile and the changes I want:

For example: “Make key 0 open my favorite website, label it Web with a blue background. Make dial 17 adjust volume left/right and mute on press. Leave everything else unchanged.”

## Profile format

- `schemaVersion`: 1.
- `name`: a name with 1–80 characters.
- `keys`: up to 14 entries, each with a unique `index` (0–13), `label`, `background` and `action`. Missing keys are blank and inactive.
- Key 13 is the double-width screen. The app selects image mode when sending a page so its firmware clock/gauges do not overlap the configured label. Its image and app preview use the wide aspect ratio.
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

This preview supports one profile at a time. Dynamic sensors, custom image files, multiple pages, automatic reconnect, custom dial-area images and automatic startup are not implemented. Physical indices, actions and display behavior still require validation on the D200X; automated checks alone do not establish device operation.
