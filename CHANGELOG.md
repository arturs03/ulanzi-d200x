# Changelog

## Unreleased

- Document the accepted C# host / Rust data-provider architecture, language-independent executable protocol draft, resource-measurement requirements and Rust/C# engineering standards.
- Add contributor entry points to README, CONTRIBUTING and AGENTS, plus the repository-scoped `d200x-development` skill.
- Include repository skills in source-ZIP exports. This does not add a provider loader, Rust implementation or live screen refresh, and does not change the installed preview.

## 0.1.0-preview.7

- Simplify controls to Start/Stop, Update screens, Check device, Keep screen on and Enable actions. Status shows Ready/Active plus action and screen state, and follows the action toggle immediately.
- Clarify editor, profile-import and file-editing labels; add hover help explaining each control's effect. Saving remains distinct from screen updates and running actions.
- Report screen writes as data sent that needs physical confirmation. Windows Apps metadata now uses the actual release version instead of an old hardcoded preview number.
- Preserve stopped/actions-off defaults, explicit uploads, transport limits and all profile/action contracts.

## 0.1.0-preview.6

- Redraw the full rounded-panel surface on resize and refresh exposed layout areas when resizing ends, addressing repeated border trails in the user's screenshot.
- Clarify that the deck is an app preview: stop listening and explicitly Send to device to update the physical screens.
- Give device PNG assets names derived from their pixels so a changed profile cannot reuse the Starter image filenames. A D200X reference implementation identifies firmware filename caching; this addresses a likely cause of the reported old page despite completed writes. Physical confirmation is still required.
- Add hardware-free regression checks for full-surface invalidation when width/height grow or shrink, changed/unchanged image filenames and manifest-to-PNG references. Actions and packet framing are unchanged.

## 0.1.0-preview.5

- Assign built-in icons or import local PNGs for LCD keys. Icons appear in the preview and in explicit device page uploads, including the wide Key 13. Missing/invalid custom icons fail page preparation before USB writes.
- Add quick presets for screenshot selection, Snipping Tool region recording, Game Bar app recording, Discord mute and audio controls. These reuse validated shortcuts; selection/saving does not execute them.
- Add Load profile with validation, a previous-profile backup, action execution disabled on import and portable custom icon copying.
- Include the recreated Studio layout draft with confirmed Discord mute, proposed screenshot/recording slots and visibly unavailable live widgets. Quotes, sensors, FPS and horn playback remain separate pending integrations.
- Bound PNG input to 2 MB and 1024 pixels per side, normalize imports to at most 512 pixels, reject external/traversal icon references and directory links, and keep decoding/rendering on CPU/GDI.

All 72 hardware-free checks pass, along with isolated PNG/editor/profile-import checks and standard/compact UI rendering. Physical icon display and capture-shortcut delivery need user testing. No screen recording is started during verification.

## 0.1.0-preview.4

- Minimal dark desktop layout with rounded controls, a clickable deck, dials/side buttons, clearer listening/keep-awake status and an activity area.
- Edit labels, colors and actions directly in the app. Configure dial left/right/press separately. Validated saves preserve other mappings; invalid edits leave the saved profile intact. Display uploads remain explicit.
- Add a shared ActionCatalog / IActionModule API for action metadata, validation and dispatch. Existing shortcuts, volume/media keys, websites and application launches use this registry; JSON schema version 1 remains compatible.
- Add hardware-free module and editing checks. No new renderer, driver, service or automatic startup is introduced.

The user reported that preview 3 no longer sleeps with keep-awake/listening enabled. This is a successful session report, not a guarantee of long-term reliability. Preview 4 still requires physical action/lifecycle checks.

## 0.1.0-preview.3

- Add an opt-in experimental Keep device awake mode for the reported one-minute return to stock graphics. It requires a profile transfer in the current app session, then sends one small image-mode/time packet every 5 seconds while listening.
- Keep the default reader-only behavior when the checkbox is off. There is no GPU polling, repeated page upload, retry or automatic reconnect.
- Bound keep-awake writes to 3 seconds. A read or write failure stops both operations; Stop/exit cancels pending work. Automated failure/cancellation checks pass.

The user physically confirmed that the Key 13 overlay is gone. Physical timeout prevention and possible mode-refresh blinking still need confirmation; the follow-up log has no keep-awake entries and the user still reports the one-minute fallback.

## 0.1.0-preview.2

- Select image mode for the special wide screen before and after an explicit page transfer, to disable retained CPU/RAM/GPU gauges behind key 13.
- Compose key 13 at its double-width aspect ratio and span its preview across two grid cells.
- Reject invalid display bundles before emitting any mode command. Mode commands are bounded to the explicit transfer; no background polling, retries or firmware flashing are added.

Protocol and desktop checks pass. The user subsequently confirmed the physical Key 13 overlay correction on preview 3.

## 0.1.0-preview.1

- Standalone Windows command-line app using native HID APIs.
- Read-only device inspection and bounded button/dial logging.
- Explicit temporary numbered display test and CPU-only PNG generation.
- Automated protocol, image and transfer checks without device access.
- Self-contained Windows x64 packaging.
- Windows desktop interface, tray controls and per-user installer/uninstaller.
- LLM-editable JSON profiles for key labels/colors, hotkeys, app launches, URLs and dial/side actions.
- Strict profile validation with last-valid-profile preservation and actions disabled initially.

Physical-test logs confirm press/release for all 14 LCD keys, both dial directions on all three dials, and both side buttons. Profile transfer completed, and the user confirmed the physical numbered labels with an overlap on key 13. Dial presses and mapped action delivery still need confirmation. This is an experimental preview, not a verified complete Studio replacement.
