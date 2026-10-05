# Stability criteria

Preventing app failures and PC crashes is a core product requirement. Feature work must preserve conservative device access and error handling. Passing automated tests does not establish crash-free operation on real hardware.

Current safeguards:

- App starts stopped; profile loading does not access the device or run actions.
- Strict JSON validation rejects unsupported fields and actions; no generated scripts or arbitrary shell commands execute.
- No custom drivers, firmware commands, administrator access, service registration or Windows security changes.
- No AMD telemetry polling or embedded browser/game rendering engine.
- Known Studio process and concurrent direct sessions are checked before active device access. Keep other third-party controllers closed, and do not launch Studio during a direct session.
- Display transfers are explicit, built before opening the writable device, and have cancellation/timeouts. There are no automatic retry or reconnect loops.
- I/O failures stop the device session. Action errors are reported separately. Physical recovery remains unverified.

Validation still required:

1. Read-only physical button/dial input, with actions disabled.
2. One explicit display transfer, including disconnection/recovery behavior.
3. Individual action mappings, then repeated sessions under ordinary workloads.
4. Stop/exit, unplug/replug and sleep/wake behavior.
5. Compare any new crash time against Windows logs. Avoid attributing a crash to this app or declaring it fixed without evidence.

Existing Windows USB/GPU drivers and hardware remain part of the system. A user-space app can exercise those components; this project cannot promise zero BSODs. The current preview has not completed the physical validation above and must not be presented as proven stable.
