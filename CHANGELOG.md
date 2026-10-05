# Changelog

## 0.1.0-preview.3

- Add an opt-in experimental Keep device awake mode for the reported one-minute return to stock graphics. It requires a profile transfer in the current app session, then sends one small image-mode/time packet every 5 seconds while listening.
- Keep the default reader-only behavior when the checkbox is off. There is no GPU polling, repeated page upload, retry or automatic reconnect.
- Bound keep-awake writes to 3 seconds. A read or write failure stops both operations; Stop/exit cancels pending work. Automated failure/cancellation checks pass.

Physical timeout prevention, possible mode-refresh blinking and overlay removal still need confirmation.

## 0.1.0-preview.2

- Select image mode for the special wide screen before and after an explicit page transfer, to disable retained CPU/RAM/GPU gauges behind key 13.
- Compose key 13 at its double-width aspect ratio and span its preview across two grid cells.
- Reject invalid display bundles before emitting any mode command. Mode commands are bounded to the explicit transfer; no background polling, retries or firmware flashing are added.

Protocol and desktop checks pass. Correction of the physical key 13 overlay awaits user testing.

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
