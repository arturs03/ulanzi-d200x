# Changelog

## 0.1.0-preview.1

- Standalone Windows command-line app using native HID APIs.
- Read-only device inspection and bounded button/dial logging.
- Explicit temporary numbered display test and CPU-only PNG generation.
- Automated protocol, image and transfer checks without device access.
- Self-contained Windows x64 packaging.
- Windows desktop interface, tray controls and per-user installer/uninstaller.
- LLM-editable JSON profiles for key labels/colors, hotkeys, app launches, URLs and dial/side actions.
- Strict profile validation with last-valid-profile preservation and actions disabled initially.

The first physical-test log confirms keys 0–6 and 10–13, both dial directions on all three dials, and both side buttons. Profile transfer completed; correct physical images, the remaining inputs and mapped action delivery still need confirmation. This is an experimental preview, not a verified complete Studio replacement.
