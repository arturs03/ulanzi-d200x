# D200X Direct

An independent Windows app for controlling a **Ulanzi D200X** directly over USB, with profiles that ChatGPT or another LLM can edit. Includes a desktop interface, tray icon, per-user installer and documented JSON configuration. No Ulanzi Studio plugins, LLM API account or scripting engine are required.

Contributors: start with [CONTRIBUTING.md](CONTRIBUTING.md), [architecture](docs/architecture.md) and [engineering standards](docs/engineering.md). AI coding agents should read [AGENTS.md](AGENTS.md); a reusable repository skill is included below.

**Experimental preview:** device enumeration, all 14 LCD key inputs, all three dials turning, both side buttons and the numbered Starter page on the physical display are verified on a D200X. Automated checks pass. Dial presses 18/19, the key 13 overlay correction and keep-awake in a user session are also confirmed. Dial 17 press, mapped actions and long-term stability remain unverified. This is not yet a verified complete Studio replacement or a fix for PC crashes.

## Install

Download `D200X-Direct-0.1.0-preview.17-win-x64-Setup.exe` from the project's release artifacts and choose **Install / Update**. Close the app through its tray menu before updating; saved profiles are preserved. Open **D200X Direct** from the Windows Start menu. The executable bundles .NET and the System data provider, so end users need no compiler/runtime installation. Preview builds are not code signed.

Target: Windows x64 supported by .NET 8, with a D200X connected by a data-capable USB cable. Only D200X is targeted; other Ulanzi models and Windows ARM64/x86 are not tested. No administrator access, driver replacement, service or automatic startup is installed.

Installation: `%LOCALAPPDATA%\D200XDirect`. Profile: `%APPDATA%\D200XDirect\profile.json`. Uninstall through Windows Settings → Apps → D200X Direct (Preview), or reopen Setup and choose Uninstall. Saved profiles are preserved.

## First use

1. Finish any benchmark or comparison that needs Studio. Fully exit Ulanzi Studio through its tray icon before starting direct device control. Do not run both controllers at once.
2. Open the app. Device control starts stopped; assigned hardware data starts automatically. **Enable actions** and **Keep screen on** default on. Turn actions off for input-only diagnostics. **Check device** only reads capabilities.
3. Click **Start** and press buttons, rotate and press the dials. Confirm decoded input appears. **Stop** releases the device; zero events does not establish successful input.
4. Use **Edit profile**, save your changes, then **Reload file**. Invalid profiles are rejected; the last valid mappings remain active.
5. While stopped, click **Update screens** to send labels/icons/colors and a snapshot of assigned readings. Data resumes automatically; preparation waits up to four seconds for output/CPU baseline. Missing/stale values show `--`. Confirm the physical screen. The numbers stay static until another explicit upload. This changes the displayed page without editing Studio's saved profiles; reopening Studio is expected to restore its page, but that recovery still needs confirmation.
6. Start to use mappings from physical controls. **Enable actions** can turn execution off when needed. The main/tray Stop pauses both device control and hardware data; the tray menu can also exit.

**Keep screen on** defaults on and sends one small image-mode/time packet every five seconds while the controller is started, without requiring a prior page upload. It does not repeatedly upload images, poll sensors or run actions. Stop/exit or an I/O error ends the updates. Stop the controller to change this setting. The user confirmed idle prevention with keep-awake in an earlier session; starting it without an upload, extended duration and possible blinking still need physical verification.

## Edit controls in the app

Select a screen key and choose **Key type**: Shortcut, Website / link, Application, Hardware data, or Label / image. The editor then asks for the shortcut, address, application or value to show. Hardware keys have an optional **When pressed** action, defaulting to No action. Assignment works while data is stopped; a screen can display content without running anything when pressed. Key 13 accepts up to three distinct values. Choose **Save changes** to save content and action together. Switching away from Hardware data removes that key's bindings when saved; choosing Label / image also clears its press action.

Preview 17 widens the mapping panel and its fields, with wrapping help text and vertical scrolling in compact windows. Choosing a temperature or GPU load shows a reminder to keep Hardware Monitor running with CSV logging enabled. CPU/RAM usage does not need that monitor.

Preview 10 fixes the dropdown `ContextMenuStrip` disposal crash. Each select owns a reusable menu; final cleanup is deferred past click/close processing. Native mouse-message checks cover selecting, clicking outside, switching menus, repeated opening/closing and removing a select during a selection callback.

Shortcut preset fills common screenshot, recording, mute and audio mappings. Choose a built-in icon or use Image… to import an image for an LCD key. Import profile imports a local profile, copies its custom PNGs and backs up your previous profile; your action toggle choice is preserved. For a dial, choose Turn left, Turn right or Press before editing. Key labels/colors/icons update on the physical display only when you explicitly **Update screens** while stopped. Changes use the same JSON profile as LLM customization; invalid edits leave it intact. **Enable actions** controls press execution separately from displayed content.

The shared [action module API](docs/actions-api.md) provides descriptors, validation and handlers for existing actions, with documented extension points for future integrations. It does not run arbitrary downloaded modules or expose a network server.

## Customize with an LLM

Screenshot region uses Win+Shift+S; region recording uses Win+Shift+R to open Snipping Tool, where you select the region and start recording. A separate Game Bar preset uses Win+Alt+R for a supported app/game. These reuse the installed Windows tools. See [Microsoft Snipping Tool documentation](https://support.microsoft.com/en-gb/windows/apps/use-snipping-tool-to-capture-screenshots) and [Game Bar recording documentation](https://support.microsoft.com/en-gb/accessibility/windows/use-a-screen-reader-to-record-your-screen-with-xbox-game-bar). Physical shortcut delivery still needs testing; no capture is started during automated checks.

The bundled [Studio layout draft](profiles/studio-layout.json) recreates the key positions and includes mute/capture mappings, with visibly unavailable live widgets. See [what remains](docs/studio-layout.md).

Give ChatGPT, Claude, another LLM or a coding assistant your profile plus [the schema](profiles/profile.schema.json) and [customization instructions](docs/customization.md). Ask it to change the layout or actions, then save and reload the resulting JSON. A local coding assistant can edit the file directly; a regular chat can return JSON for you to paste. No recompilation is needed.

Supported mappings: hotkeys/media keys, local application launches, HTTP/HTTPS links, dial left/right/press and side buttons. Labels, key background colors and built-in/custom PNG icons are configurable. Actions do not run on profile load. Arbitrary scripts, shell commands and application arguments are not supported.

[profiles/starter.json](profiles/starter.json) has inactive controls. [profiles/example.json](profiles/example.json) demonstrates a website, media playback, a Discord mute hotkey and a volume dial. Discord must have its matching keybind configured separately.

## Established development direction

Agreed on 2026-10-05: preserve the working **C#/.NET Windows UI and USB controller**, use **Rust for bundled data providers**, and define a **language-independent executable plugin protocol**. Low resource use, reliable stop behavior and honest unavailable/stale readings are product requirements. Community providers may use Go or another language that implements the protocol.

| Area | Current implementation | Planned extension |
| --- | --- | --- |
| Host | C#/.NET 8, WinForms, CPU/GDI rendering, direct HID | Own plugin lifecycle, validation, widget rendering and device scheduling |
| Actions | Compiled `IActionModule` registry and validated JSON profiles | Keep actions separate from read-only data providers |
| Live values | Rust CPU/RAM usage, selected OHM-log temperatures/GPU load and typed app-preview bindings | Markets and optional FPS |
| Plugin installation | Metadata discovery and automatic assigned-provider collection | Generic dynamic source/settings selection and archive/update UI |
| Display refresh | Explicit full-page upload; optional keep-screen-on packets | Opt-in bounded updates, after physical verification of the transport |

**Preview 12 automatically collects saved hardware assignments and defaults actions/keep-awake on; continuous physical screen refresh is pending.** Choose Hardware data and a value, then Save changes. For CPU/GPU/hotspot temperatures, configure **Sensor source…** once. The [provider setup](docs/providers.md) explains source format/freshness limits. The [plugin protocol](docs/plugin-protocol.md) is experimental. Real-temperature verification, real GPU-load verification, markets/FPS, generic settings enumeration and credential delivery remain pending. Rust does not remove external collector or driver costs. This architecture is for D200X Direct; Studio uses a separate SDK/package contract.

Save Hardware data in the key editor to collect assigned values automatically. Opening the app with saved assignments, profile import/reload and source changes also collect automatically. Main/tray **Stop** pauses collection; **Start**, **Update screens**, profile reload or a saved data/source change resumes it. Assignment needs no live reading. The app shows values in tile details; **Update screens** sends a physical snapshot of readings. Continuous physical refresh remains disabled. **Test selected screen** checks a small-image transport candidate on the D200X; see the [physical procedure](docs/display-transport.md) and [provider instructions](docs/providers.md).

Read [architecture](docs/architecture.md) for ownership and rollout, [plugin protocol](docs/plugin-protocol.md) for the proposed folder/IPC contract, [engineering standards](docs/engineering.md) for Rust/C# practices, and [performance requirements](docs/performance.md) for measurement and resource controls.

The [current deck implementation plan](docs/live-widgets-plan.md) tracks all eight missing-data tiles (ten values), including the implemented CPU/RAM preview slice and remaining source/device verification gates.

The [plugin delivery and open-source plan](docs/plugin-delivery-plan.md) proposes shared system/markets/optional FPS provider packages, community authoring/conformance tools, release packaging and migration away from the older Studio sensor bridge. Node.js was that Studio plugin's build tool, not its hardware collector. The planned Direct providers need no Node runtime; temperature/GPU values still require a supported sensor source. The document also records license/provenance and clean-source release checks before publication.

The checked-in [d200x-development skill](.agents/skills/d200x-development/SKILL.md) routes AI contributors through these same documents. In Codex, invoke `$d200x-development` from this repository; repository skills live under `.agents/skills` ([official documentation](https://learn.chatgpt.com/docs/build-skills)). Other agents can read the skill as Markdown. No personal skill installation is required for a checkout.

## Build from source

Use Windows, PowerShell, Git (optional) and the **.NET 8 SDK**:

```powershell
# From this repository's root:
./build.ps1
./run.ps1 -Mode self-test
dotnet run --project Desktop/D200xDirect.App.csproj -c Release

# Provider/release work additionally requires the pinned Rust toolchain and C++ x64 tools:
./build-providers.ps1
dotnet out/D200xDirectController.dll provider-check out/provider-packages/fixture

# Create a self-contained x64 installer, source ZIP and SHA-256 checksums:
./package.ps1
```

Publishing may download Microsoft's runtime build packs into the normal NuGet cache. It installs no system runtime or driver. Generated files are ignored by Git. `releases` contains the distributable artifacts; `out` contains build outputs.

The diagnostic CLI remains available:

```powershell
./run.ps1 -Mode inspect
./run.ps1 -Mode listen -Seconds 60
./run.ps1 -Mode test-page -Seconds 60
./run.ps1 -Mode help
```

`test-page` sends a temporary TEST 00–13 page. Other diagnostic commands do not write to the device. Self-tests access no hardware, send no keys and launch no applications.

## Verification and limits

Stability is a core requirement: preventing application errors, black screens and BSODs takes priority over new features. The app uses existing Windows APIs without adding a kernel driver, firmware changes, elevation or Windows security changes. These choices reduce the scope of low-level changes; they cannot guarantee that existing drivers or hardware will never fail. See [the stability criteria](docs/stability.md).

- Observed D200X: VID `2207`, PID `0019`, compatible input/output reports of 1025 bytes including Windows report ID 0. The separate keyboard interface is not used.
- Input decoding, report framing, ZIP transfer boundaries/reassembly, PNG generation, JSON validation and action selection have automated checks.
- Physical-test evidence: all 14 keys (0–13) have press/release events; dials 17–19 turn both ways; side buttons 15–16 have press/release events. Dial presses 18/19 are confirmed; dial 17 press remains to be checked. See [hardware validation](docs/hardware-validation.md).
- A Starter profile display transfer completed, and the user confirmed the correct physical numbered labels. Action delivery, reconnect behavior, sleep/wake and long-term reliability remain unverified.
- Preview 1 left the built-in CPU/RAM/GPU gauges overlapping key 13. Preview 2 explicitly selects image mode during page transfer and formats key 13 as the double-width screen. The user physically confirmed its correction on preview 3. These are ordinary display commands; no firmware flashing or sensor polling is added.
- One active profile with JSON/PNG import/export and optional CPU/RAM/GPU load/temperature preview bindings; no market/FPS acquisition, physical live-value refresh, multiple device pages, custom dial-area display, automatic reconnect or automatic startup yet.
- The app refuses active device access while the known Studio process runs and prevents concurrent sessions with its diagnostic CLI. Keep other third-party controllers closed too.
- The desktop app uses standard Windows controls and CPU/GDI image generation. It does not poll AMD telemetry or initialize a browser/game rendering engine. This does not guarantee protection against GPU driver, USB, kernel or hardware failures.

See [CONTRIBUTING.md](CONTRIBUTING.md) for reporting hardware results. Redact device-specific USB paths before sharing diagnostic output. Keep personal logs and profiles out of source archives.

## License and references

Controller and app source: [MIT](LICENSE). Bundled .NET runtime notices are included in the installer payload. Protocol references and independent-project attribution are in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). This project is not affiliated with Ulanzi.
