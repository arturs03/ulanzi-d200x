---
name: d200x-development
description: Implement or review D200X Direct host, provider, profile, build or documentation changes using the repository's established C# and Rust architecture, resource limits and device-safety invariants. Use for this independent controller; official Ulanzi Studio plugins use a different contract.
---

# D200X Direct development

Use this skill for changes to this repository. Resolve the repository root three directories above this skill folder. Read [AGENTS.md](../../../AGENTS.md) and inspect the actual source/status before editing; do not assume the design draft is implemented or a sibling sensor project is available.

## Established decisions

Keep the C#/.NET 8 WinForms host and direct HID controller. Prefer Rust for bundled read-only data providers, communicating as supervised executables through bounded, versioned JSON. The protocol remains language-independent for community providers. Preview 8 adds the initial supervisor, Rust workspace and CPU/RAM app preview; inspect current status and read provider setup before assuming other sources/settings or live screen refresh exist.

Data collection, widget rendering, actions and USB ownership are separate. Providers do not receive a device writer or an action interface. Plugin copying/discovery and standalone parsing cannot execute code; the shown app automatically runs providers for saved hardware assignments. Stop, exit, disconnect, deadline and process failure need explicit ownership and cleanup; process separation is not an OS sandbox.

Preview 12 follows the user's updated defaults: assigned hardware data runs automatically without a separate Start data control; saved binding/source changes replace collection after owned cleanup. Main/tray Stop pauses it. Enable actions and Keep screen on default on; importing profiles preserves user toggle choices. Device control and page uploads still require their own explicit UI actions. Preserve display-only hardware keys and imported/custom bindings; see provider instructions for current lifecycle behavior.

Preview 11 adds one shared bounded OHM-format log reader for selected CPU package/GPU core/hotspot temperatures. Private local settings are configured in Sensor source and delivered only in system-provider hello requests. Exact sensors, original observation time, same-GPU identity, daily rollover and DST/freshness checks are required. Test with synthetic CSVs via sensor-log-check; do not start a monitor or query real hardware during automated validation. Real-temperature, resource and physical live-refresh evidence remain pending.

## Read only the relevant guidance

Preview 16 adds GPU Core load in percent to the same monitor-log reader and private source settings. Keep exact load versus temperature IDs, 0–100 range, same-GPU identity and source timestamps. An assigned GPU load can match a unique load sensor on the already configured GPU; ambiguous matches require manual selection. Mapping/header reads never start a monitor. Missing current-day/stale/sensor states are diagnosed separately from provider failure; see the provider instructions.

Preview 14 renders assigned samples into explicit screen snapshots and adds one bounded selected-LCD transport probe. Update screens resumes paused data and waits briefly for output/CPU baseline; stale/missing values remain unavailable. Do not promote synthetic rendering/0x000d framing checks to physical D200X evidence. Follow [display transport](../../../docs/display-transport.md) before any continuous refresh work; its physical verification gate remains required.

- Architecture/lifecycle or new integrations: [architecture](../../../docs/architecture.md) and [protocol draft](../../../docs/plugin-protocol.md).
- Rust or C# code: relevant language section and validation guidance in [engineering standards](../../../docs/engineering.md).
- Acquisition, dependencies, caching, rendering or scheduling: [resource requirements](../../../docs/performance.md).
- Profile/public fields: [customization](../../../docs/customization.md), [schema](../../../profiles/profile.schema.json) and existing parser/editor; change these together and retain old-profile compatibility.
- Action dispatch: [action API](../../../docs/actions-api.md).
- Device behavior: [stability](../../../docs/stability.md) and [physical evidence](../../../docs/hardware-validation.md).

## Implementation expectations

Validate before mutation; preserve the last valid configuration, default-on action/keep-awake preferences and stopped device control. Keep code changes focused, with small external adapters and testable parsing/formatting. Bound IPC, I/O, queues, caches and diagnostics; share acquisition across related tiles. Use trustworthy source timestamps and exact selections; stale/missing/ambiguous samples show unavailable, never fabricated values. Keep stdout for provider protocol only and secrets outside profiles/manifests/logs.

Do not implement live USB refresh by repeatedly uploading full pages. Current uploads remain explicit; verify an update primitive physically before enabling future opt-in refresh. Preserve Studio/concurrent-controller guards, HID capability selection, cancellable writes and stop-on-I/O-error behavior. Driver installation, elevation, services, startup/security changes and new low-level telemetry setup are separate decisions, not implied by editing a provider.

For C# changes use `./build.ps1` and relevant isolated UI checks. For Rust use the documented fmt/clippy/test/release commands only after a workspace/toolchain exists. Tests must use fixtures, not real USB, live credentials/markets, keyboard injection or capture. For docs-only work check links, status claims and public-data hygiene. If validation cannot run, say what remains unverified.

Report observable behavior, compatibility, checks and material limitations. Record real hardware observations separately from automated results. Update the authoritative docs when decisions change; keep this skill as a workflow/router instead of duplicating every protocol limit or historical report.
