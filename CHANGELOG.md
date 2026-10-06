# Changelog

## Unreleased

- Add a staged implementation plan for the Studio layout's eight missing-data tiles (ten values), including shared monitor-log acquisition, source selection, preview/device verification and resource checks.

- Document the accepted C# host / Rust data-provider architecture, language-independent executable protocol draft, resource-measurement requirements and Rust/C# engineering standards.
- Add contributor entry points to README, CONTRIBUTING and AGENTS, plus the repository-scoped `d200x-development` skill.
- Include repository skills in source-ZIP exports. This does not add a provider loader, Rust implementation or live screen refresh, and does not change the installed preview.

## 0.1.0-preview.17

- Widen the key mapping panel and resize its inputs/icon picker to available width. Wrap help text, preserve the deck's working space and retain vertical scrolling in compact windows.
- Show a reminder directly below Hardware data key type when temperatures or GPU load are selected: the hardware monitor must remain running with CSV logging enabled. CPU/RAM usage alone does not show this dependency.
- Verify dependency visibility, normal/compact layout overflow and existing editor/dropdown/lifecycle behavior with isolated fixtures. CPU temperature remains unavailable when the external monitor cannot expose it; this UI change does not alter Windows security or install a driver.

## 0.1.0-preview.16

- Add GPU Core load (`gpu.usage`, percent) to the existing shared bounded monitor-log snapshot. It has its own exact load identifier, 0–100 range and original observation timestamp; GPU memory load is not substituted. All configured GPU readings must refer to one GPU.
- Add GPU load to the Hardware data editor/source picker and the Studio layout's CPU/RAM/GPU composite. Assigning it with an existing source automatically saves a unique GPU Core load match on the configured GPU; ambiguous sources remain unavailable. Matching is cancellable/bounded and reads names only.
- Explain missing current-day logs, stale rows, sensor errors and source recovery in Activity without printing paths/provider text or repeating unchanged status every second. A saved mapping is not an active monitor; no monitor or logging is started/configured automatically.
- Preserve collection when identical source settings are saved. Report Stop cancellation normally and distinguish the 30-second screen deadline. Correct the keep-awake log: its periodic signals do not refresh displayed values.
- Extend synthetic Rust/C# integration, range/unit/freshness, same-GPU selection and three-value UI checks. Real monitor values, collector overhead and physical refresh remain unverified.

## 0.1.0-preview.15

- Read sensor names automatically when opening a saved source or browsing to a log folder. For new sources, preselect a unique CPU Package and unique GPU Core / GPU Hot Spot on one GPU; multiple GPUs and duplicate roles require manual selection. Existing exact choices, including unconfigured roles, are preserved.
- Keep source selection separate from live acquisition: historical headers can supply names but stale readings remain unavailable. Add synthetic checks for automatic matches, ambiguous GPUs/CPU/roles, invalid IDs, saved omissions and folder changes. No monitor is started or installed.

## 0.1.0-preview.14

- Include assigned hardware readings in explicitly uploaded images: numeric tiles and up to three independent values on key 13. Missing/stale/future readings show `--`; labels/icons/colors/actions are preserved and readings stay out of profile JSON.
- Update screens resumes assigned data and waits up to four seconds for output/CPU baseline before preparing a snapshot. The uploaded numbers stay static until another explicit upload; this is not continuous refresh.
- Add Test selected screen after a page upload: one saved LCD key, temporary TEST label, 64 KiB bounded image ZIP and documented D200 command 0x000d. Its D200X compatibility is unverified. Existing stopped-session, exclusive-controller, timeout/cancellation and Studio guards remain.
- Add synthetic rendering, timestamp/independent-value, immutable-profile, content-name, key 10/13 framing, resumed capture/cancellation and probe-state checks. Physical snapshot/probe results remain pending; no USB is used in automated checks.

## 0.1.0-preview.13

- Fix automatic System data stopping after nominal one-second delays. Windows timers can wake early; the supervisor now waits cancellably for the remaining monotonic minimum interval before sending a snapshot, keeping one request owned throughout the wait. The provider's sampling limits remain intact.
- Report startup/snapshot stage and fixed, sanitized failure reasons instead of the ambiguous "System data data unavailable" message. Provider output, paths and raw exception messages stay out of UI diagnostics.
- Add fixture regressions for immediate/early/nominal cadence, concurrent request rejection, cancellation during cadence, response deadlines and owned cleanup. Physical live display refresh and real temperature/source verification remain pending.

## 0.1.0-preview.12

- Automatically collect saved Hardware data assignments on app opening, save, profile import/reload and sensor-source changes. Remove the separate Start data/Stop data and AI guide buttons.
- Serialize and coalesce assignment changes; join old providers before replacing them. Remove unused collection, preserve shared providers, and keep failed providers unavailable without automatic restart loops. Main/tray Stop pauses data; Start or saved configuration changes resume it.
- Default Enable actions and Keep screen on to on. Preserve action-toggle choices on import; device input and page uploads remain explicit. Keep-awake starts with controller Start without requiring a previous page upload.
- Add fixture-only automatic startup/save/shared-demand/rapid-edit/removal/stop/failure/exit checks and verify default toggles/import behavior. Real-device behavior of the revised defaults and desktop/collector resource measurements remain unverified.

## 0.1.0-preview.11

- Add assignable CPU package, GPU core and GPU hotspot temperatures, including display-only assignment while stopped and independent unavailable values on key 13. The Studio layout draft now binds its three temperature tiles without embedding local sensor identifiers.
- Read all selected temperatures from one bounded shared Open Hardware Monitor CSV snapshot. Preserve source timestamps, use this computer's Windows time zone, reject ambiguous/nonexistent DST times, require today's daily log and expire readings after five seconds. No core-to-hotspot substitution or automatic monitor startup.
- Add explicit sensor-source selection, with exact CPU/GPU identifiers, same-GPU validation and separate private per-user settings. Deliver settings only to the bundled log-capable system provider at hello; changing the source stops collection.
- Add synthetic CSV/IPC/cleanup and source-editor tests, plus malformed/stale/partial-write/DST checks. Real sensor verification, desktop/monitor performance measurements and physical live refresh remain pending.

## 0.1.0-preview.10

- Fix the `ContextMenuStrip` ObjectDisposedException when selecting dropdown items: keep one menu per select instead of disposing synchronously in Closed. Defer final menu disposal when its owner is removed, including removal from an item callback.
- Refresh/dispose previous menu items on reopening so changed choices have current handlers without accumulating menu windows. Empty dropdowns no longer open or try to navigate nonexistent items.
- Add native mouse-message regression checks for selection, outside clicks, switching dropdowns, 30 reopen/close cycles, repopulated choices and owner cleanup. The original synchronous-disposal code reproduces the reported exception in an isolated negative control; the fix passes the same native item-click path.

## 0.1.0-preview.9

- Replace the separate Live preview assignment window with an inline Key type editor: Shortcut, Website / link, Application, Hardware data or Label / image. Choose content first, then optional press behavior for hardware keys. Display-only keys need no action.
- Assign CPU/RAM while stopped or unavailable; use up to three distinct values on key 13. Content, appearance and action save together, with validation and a previous-profile backup. Existing bindings, missing providers and custom labels/precision are retained; changing type removes bindings on save.
- Add inline Start data / Stop data. Query only assigned values through one shared worker per provider; bounded UI delivery, freshness, failure isolation and owned cleanup remain enforced. Changed bindings stop the data session until explicitly restarted.
- Add isolated editor checks and repeated fixture-only desktop start/stop checks. Physical live-value refresh remains pending; explicit USB uploads still send saved labels/icons.

## 0.1.0-preview.8

- Add explicit live app previews through a bounded C# executable-provider supervisor, metadata-only discovery, fixed-source manifests and a shared Rust protocol crate. Deadlines, malformed messages, crashes and Stop/exit end the owned process tree; providers have no USB/action interface.
- Bundle a disabled Rust CPU/RAM usage provider. CPU needs two samples; unsupported processor-group behavior stays unavailable. Temperature/GPU, markets/FPS, dynamic source/settings enumeration and physical live refresh remain pending.
- Add optional typed v1 profile widget bindings, one per normal LCD key and up to three on key 13, preserving labels/icons/actions. Assign/clear bindings in Live preview; stale/disabled/missing components show `--`. Older profiles still load; older apps reject the new field.
- Add schemas, pinned toolchain/locked dependencies, hardware-free Rust/C# lifecycle fixtures, Windows CI configuration and provider licenses/notices. Source exports include all new source/schema/CI paths; host-only builds still need no Rust.
- Existing explicit USB uploads/keep-screen-on behavior are unchanged. Current uploads send saved labels/icons, not live samples. No old bridge/source deletion, installed-app update or repository publication is performed by building this release.

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
