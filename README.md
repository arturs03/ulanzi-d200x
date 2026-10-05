# D200X Direct

An independent Windows app for controlling a **Ulanzi D200X** directly over USB, with profiles that ChatGPT or another LLM can edit. Includes a desktop interface, tray icon, per-user installer and documented JSON configuration. No Ulanzi Studio plugins, LLM API account or scripting engine are required.

**Experimental preview:** device enumeration, all 14 LCD key inputs, all three dials turning, both side buttons and the numbered Starter page on the physical display are verified on a D200X. Automated checks pass. Dial presses 18/19, the key 13 overlay correction and keep-awake in a user session are also confirmed. Dial 17 press, mapped actions and long-term stability remain unverified. This is not yet a verified complete Studio replacement or a fix for PC crashes.

## Install

Download `D200X-Direct-0.1.0-preview.5-win-x64-Setup.exe` from the project's release artifacts and choose **Install / Update**. Close the app through its tray menu before updating; saved profiles are preserved. Open **D200X Direct** from the Windows Start menu. The executable bundles .NET, so end users do not need to install a runtime separately. Preview builds are not code signed.

Target: Windows x64 supported by .NET 8, with a D200X connected by a data-capable USB cable. Only D200X is targeted; other Ulanzi models and Windows ARM64/x86 are not tested. No administrator access, driver replacement, service or automatic startup is installed.

Installation: `%LOCALAPPDATA%\D200XDirect`. Profile: `%APPDATA%\D200XDirect\profile.json`. Uninstall through Windows Settings → Apps → D200X Direct (Preview), or reopen Setup and choose Uninstall. Saved profiles are preserved.

## First use

1. Finish any benchmark or comparison that needs Studio. Fully exit Ulanzi Studio through its tray icon before starting direct device control. Do not run both controllers at once.
2. Open the app. It starts stopped, with configured actions disabled. **Inspect USB** only reads capabilities.
3. Click **Start listening** and press buttons, rotate and press the dials. Confirm decoded input appears. **Stop listening** releases the device; zero events does not establish successful input.
4. Use **Edit JSON**, save your changes, then **Reload**. Invalid profiles are rejected; the last valid mappings remain active.
5. While stopped, click **Send to device** to send the key labels/colors. Confirm the actual physical screen. This changes the displayed page without editing Studio's saved profiles; reopening Studio is expected to restore its page, but that recovery still needs confirmation.
6. Start listening and check **Run actions** to execute mappings from physical controls. The tray menu can stop control or exit.

If the device returns to its stock screen after about one minute, send the profile first, select **Keep awake** while stopped, then **Start listening**. This opt-in setting sends one small image-mode/time packet every five seconds; it does not repeatedly upload images, poll sensors or run actions. Stop/exit or an I/O error ends the updates. The setting defaults off and cannot be changed during a session. The user confirmed that the device stays awake in a session with this enabled. Exact extended duration and possible blinking still need testing; listening alone does not send these updates.

## Edit controls in the app

The dark desktop interface has a clickable deck and an action inspector. Select a key, dial or side button, edit its mapping, then choose **Save mapping**. Quick setup fills common screenshot, recording, mute and audio mappings. Choose a built-in icon or use PNG… to import an image for an LCD key. Load profile imports a local profile, copies its custom PNGs and backs up your previous profile; actions stay disabled until enabled. For a dial, choose Turn left, Turn right or Press before editing. Key labels/colors/icons update on the physical display only when you explicitly **Send to device** while stopped. Changes are saved to the same JSON profile used by LLM customization; invalid edits leave the saved profile intact. The app's **Run actions** toggle controls execution.

The shared [action module API](docs/actions-api.md) provides descriptors, validation and handlers for existing actions, with documented extension points for future integrations. It does not run arbitrary downloaded modules or expose a network server.

## Customize with an LLM

Screenshot region uses Win+Shift+S; region recording uses Win+Shift+R to open Snipping Tool, where you select the region and start recording. A separate Game Bar preset uses Win+Alt+R for a supported app/game. These reuse the installed Windows tools. See [Microsoft Snipping Tool documentation](https://support.microsoft.com/en-gb/windows/apps/use-snipping-tool-to-capture-screenshots) and [Game Bar recording documentation](https://support.microsoft.com/en-gb/accessibility/windows/use-a-screen-reader-to-record-your-screen-with-xbox-game-bar). Physical shortcut delivery still needs testing; no capture is started during automated checks.

The bundled [Studio layout draft](profiles/studio-layout.json) recreates the key positions and includes mute/capture mappings, with visibly unavailable live widgets. See [what remains](docs/studio-layout.md).

Give ChatGPT, Claude, another LLM or a coding assistant your profile plus [the schema](profiles/profile.schema.json) and [customization instructions](docs/customization.md). Ask it to change the layout or actions, then save and reload the resulting JSON. A local coding assistant can edit the file directly; a regular chat can return JSON for you to paste. No recompilation is needed.

Supported mappings: hotkeys/media keys, local application launches, HTTP/HTTPS links, dial left/right/press and side buttons. Labels, key background colors and built-in/custom PNG icons are configurable. Actions do not run on profile load. Arbitrary scripts, shell commands and application arguments are not supported.

[profiles/starter.json](profiles/starter.json) has inactive controls. [profiles/example.json](profiles/example.json) demonstrates a website, media playback, a Discord mute hotkey and a volume dial. Discord must have its matching keybind configured separately.

## Build from source

Use Windows, PowerShell, Git (optional) and the **.NET 8 SDK**:

```powershell
# From this repository's root:
./build.ps1
./run.ps1 -Mode self-test
dotnet run --project Desktop/D200xDirect.App.csproj -c Release

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
- One active profile with import/export via JSON and optional PNG files; no dynamic sensors, multiple device pages, custom dial-area display, automatic reconnect or automatic startup yet.
- The app refuses active device access while the known Studio process runs and prevents concurrent sessions with its diagnostic CLI. Keep other third-party controllers closed too.
- The desktop app uses standard Windows controls and CPU/GDI image generation. It does not poll AMD telemetry or initialize a browser/game rendering engine. This does not guarantee protection against GPU driver, USB, kernel or hardware failures.

See [CONTRIBUTING.md](CONTRIBUTING.md) for reporting hardware results. Redact device-specific USB paths before sharing diagnostic output. Keep personal logs and profiles out of source archives.

## License and references

Controller and app source: [MIT](LICENSE). Bundled .NET runtime notices are included in the installer payload. Protocol references and independent-project attribution are in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). This project is not affiliated with Ulanzi.
