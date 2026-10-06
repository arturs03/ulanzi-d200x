# Stability criteria

Preventing app failures and PC crashes is a core product requirement. Feature work must preserve conservative device access and error handling. Passing automated tests does not establish crash-free operation on real hardware.

Current safeguards:

- Preview 14 includes assigned values in explicit page snapshots and a one-shot single-LCD 0x000d transport probe after a base upload. The probe is capped at 64 KiB, validates before writes and cannot overlap controller listening/keep-awake. It changes the selected image temporarily without changing the profile or actions. Snapshot/probe physical results are pending; continuous refresh stays disabled. See [display procedure](display-transport.md).

- App starts stopped; profile loading does not access the device or run actions.
- Preview 12 defaults Enable actions and Keep screen on to on at the user's request. Mapped actions still require actual physical input during controller Start. Saved hardware bindings automatically run trusted read-only providers; serialized replacement, Stop/exit cleanup and failure without restart loops have fixture checks. Automated UI checks suppress real acquisition and use purpose-built providers only.
- Strict JSON validation rejects unsupported fields and actions; no generated scripts or arbitrary shell commands execute.
- No custom drivers, firmware commands, administrator access, service registration or Windows security changes.
- No AMD telemetry polling or embedded browser/game rendering engine.
- Known Studio process and concurrent direct sessions are checked before active device access. Keep other third-party controllers closed, and do not launch Studio during a direct session.
- Display transfers are explicit, built before opening the writable device, and have cancellation/timeouts. There are no automatic retry or reconnect loops.
- Keep screen on defaults on. A started controller sends at most one small image-mode/time update every 5 seconds, with a 3-second write deadline; prior page transfer is no longer required. Failure on either reader or writer cancels both, without retries. This adds no telemetry polling or repeated ZIP uploads. Earlier sessions confirmed idle prevention; the revised startup behavior, extended duration and possible flicker remain physically unverified.
- Preview 4 uses CPU/GDI drawing for its dark interface. The action editor validates a copy before replacing the saved profile and never executes a mapping on save. Execution uses the same module validation and honors cancellation before entering Windows capability calls. Modules are compiled app code, not a sandbox for arbitrary third-party code; see docs/actions-api.md.
- I/O failures stop the device session. Action errors are reported separately. Physical recovery remains unverified.
- Preview 5 bounds PNG files to 2 MB and dimensions to 1024 pixels before native decoding, normalizes imports to at most 512 pixels per side, rejects remote/traversal references and linked icon directories/files, and renders icons on CPU/GDI. Every page image is prepared before opening the writable device. Profile imports validate configuration/assets, back up the previous profile and disable actions. Capture presets use Windows shortcuts only; automated checks never start captures.

Validation still required:

1. Complete physical input coverage: dial 17 press remains unverified; dial presses 18 and 19 are confirmed. Press/release for all 14 LCD keys, all dial rotations and both side buttons are confirmed; the Starter mappings were all inactive.
2. The numbered Starter labels on the physical display are confirmed. Check disconnection/recovery behavior next.
3. Individual action mappings, then repeated sessions under ordinary workloads.
4. Stop/exit, unplug/replug and sleep/wake behavior.
5. Compare any new crash time against Windows logs. Avoid attributing a crash to this app or declaring it fixed without evidence.

Existing Windows USB/GPU drivers and hardware remain part of the system. A user-space app can exercise those components; this project cannot promise zero BSODs. The current preview has not completed the physical validation above and must not be presented as proven stable.
