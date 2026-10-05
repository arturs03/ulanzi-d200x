# D200X Direct development

This is an independent Windows app, CLI diagnostic tool and per-user installer for the Ulanzi D200X. Keep profiles editable by any LLM through the documented JSON format; changing a profile must not require recompilation or an LLM API account.

## Stability requirement

Preventing application failures, system crashes, black screens and BSODs is a core product requirement, explicitly reinforced on 2026-10-05. Favor stability over additional features. Never claim that user-space operation or passing automated checks guarantees that Windows, USB/GPU drivers or hardware cannot crash.

- Keep device interaction conservative: explicit display writes, cancellable I/O, stop on I/O failure/disconnection, no automatic retry/reconnect loops and no simultaneous Studio control.
- Avoid custom kernel drivers, firmware changes, direct hardware register access, GPU telemetry polling, elevation and Windows security changes. Any future need for those is a separate setup decision.
- Keep LLM customization inside validated declarative JSON profiles. Do not execute generated scripts or arbitrary command strings.
- Verify physical input before display transfers and actions, then assess repeated sessions and sleep/wake behavior. Do not mark the app stable based on compilation, UI rendering or a short successful run.

## Workflow

- Read README.md, docs/customization.md and profiles/profile.schema.json before changing public configuration.
- Build with ./build.ps1. Generate distributables with ./package.ps1. Use the existing .NET 8 SDK; do not add drivers or system dependencies.
- Root C# files implement HID/protocol/config validation and CLI checks. Desktop contains the WinForms app; Installer contains the setup app. out and releases are generated and ignored by Git.
- Keep the default app stopped, with configured actions disabled. Loading a profile must not launch apps, send keys or write to a device. Display transfers require an explicit UI action.
- Preserve strict profile validation, native HID capability selection, concurrent-controller guards and the Studio-running check. Do not silently add shell/script actions, firmware writes, services or automatic startup.
- Installer operations are per user. Preserve profiles on update/uninstall. Validate fixed installation paths and reject directory links before removal. Never remove shared .NET caches or unrelated applications.
- Distinguish automated tests and app rendering from physical device verification. On 2026-10-05 the supplied physical-test logs confirmed press/release for all 14 LCD keys (0–13), all three dials rotating in both directions, and both side buttons. Dial presses remain absent. A 21,203-byte Starter display transfer completed, and the user confirmed physical numbered labels while reporting an overlap on key 13. Mapped actions, lifecycle recovery and long-term stability remain unverified. See docs/hardware-validation.md.
- Public source and release files must contain no user-specific profiles, device identifiers, personal logs, crash dumps or credentials.
- The user reported a retained CPU/RAM/GPU display underneath key 13 in preview 1. Key 13 is the double-width firmware-rendered screen. Preview 2 adds normal display-mode command 0x0006 selecting image mode 2 around each explicit page transfer, and corrects its image/preview aspect ratio. Automated checks pass; the physical overlay correction remains pending. Do not treat these display commands as firmware flashing or turn them into continuous telemetry updates.
- The user then reported that the page returns to stock Ulanzi graphics after about one minute. Installed version was still preview 1 at the first inspection; the device was present with OK status and no relevant recent System events were found. Cause remains unconfirmed. Preview 3 provides an opt-in experimental Keep device awake checkbox: after an explicit page transfer, active listening can send one image-mode/time packet every 5 seconds. It defaults off, uses no telemetry or repeated ZIP uploads, stops both I/O paths on any failure, and does not reconnect/retry. Timing and cancellation have automated tests; physical timeout prevention and possible flicker remain unverified.
