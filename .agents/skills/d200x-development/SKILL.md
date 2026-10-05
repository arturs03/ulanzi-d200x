---
name: d200x-development
description: Implement or review D200X Direct host, provider, profile, build or documentation changes using the repository's established C# and Rust architecture, resource limits and device-safety invariants. Use for this independent controller; official Ulanzi Studio plugins use a different contract.
---

# D200X Direct development

Use this skill for changes to this repository. Resolve the repository root three directories above this skill folder. Read [AGENTS.md](../../../AGENTS.md) and inspect the actual source/status before editing; do not assume the design draft is implemented or a sibling sensor project is available.

## Established decisions

Keep the C#/.NET 8 WinForms host and direct HID controller. Prefer Rust for bundled read-only data providers, communicating as supervised executables through bounded, versioned JSON. The protocol remains language-independent for community providers. No runtime plugin loader or Rust workspace exists in the documented baseline; inspect whether subsequent work has added them.

Data collection, widget rendering, actions and USB ownership are separate. Providers do not receive a device writer or an action interface. Plugin copying/discovery/profile loading cannot execute code. Stop, exit, disconnect, deadline and process failure need explicit ownership and cleanup; process separation is not an OS sandbox.

## Read only the relevant guidance

- Architecture/lifecycle or new integrations: [architecture](../../../docs/architecture.md) and [protocol draft](../../../docs/plugin-protocol.md).
- Rust or C# code: relevant language section and validation guidance in [engineering standards](../../../docs/engineering.md).
- Acquisition, dependencies, caching, rendering or scheduling: [resource requirements](../../../docs/performance.md).
- Profile/public fields: [customization](../../../docs/customization.md), [schema](../../../profiles/profile.schema.json) and existing parser/editor; change these together and retain old-profile compatibility.
- Action dispatch: [action API](../../../docs/actions-api.md).
- Device behavior: [stability](../../../docs/stability.md) and [physical evidence](../../../docs/hardware-validation.md).

## Implementation expectations

Validate before mutation; preserve last valid configuration and stopped/actions-off defaults. Keep code changes focused, with small external adapters and testable parsing/formatting. Bound IPC, I/O, queues, caches and diagnostics; share acquisition across related tiles. Use trustworthy source timestamps and exact selections; stale/missing/ambiguous samples show unavailable, never fabricated values. Keep stdout for provider protocol only and secrets outside profiles/manifests/logs.

Do not implement live USB refresh by repeatedly uploading full pages. Current uploads remain explicit; verify an update primitive physically before enabling future opt-in refresh. Preserve Studio/concurrent-controller guards, HID capability selection, cancellable writes and stop-on-I/O-error behavior. Driver installation, elevation, services, startup/security changes and new low-level telemetry setup are separate decisions, not implied by editing a provider.

For C# changes use `./build.ps1` and relevant isolated UI checks. For Rust use the documented fmt/clippy/test/release commands only after a workspace/toolchain exists. Tests must use fixtures, not real USB, live credentials/markets, keyboard injection or capture. For docs-only work check links, status claims and public-data hygiene. If validation cannot run, say what remains unverified.

Report observable behavior, compatibility, checks and material limitations. Record real hardware observations separately from automated results. Update the authoritative docs when decisions change; keep this skill as a workflow/router instead of duplicating every protocol limit or historical report.
