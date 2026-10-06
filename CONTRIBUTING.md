# Contributing

Start with [README.md](README.md), [architecture](docs/architecture.md) and [engineering standards](docs/engineering.md). AI contributors must also read [AGENTS.md](AGENTS.md); the checked-in [d200x-development skill](.agents/skills/d200x-development/SKILL.md) routes to the same guidance.

## Current build and planned Rust work

Use Windows and the .NET 8 SDK, then run `./build.ps1` from the repository root. There are currently no third-party NuGet package references. Automated core checks run as part of the build and can be repeated with `./run.ps1 -Mode self-test`. UI/icon/import changes also use the isolated desktop `--ui-check` path and visual inspection; see [engineering standards](docs/engineering.md).

The C# host now has an experimental provider supervisor/preview UI; the Rust workspace supplies protocol helpers, a failure fixture and CPU/RAM system usage. Read [provider build/use instructions](docs/providers.md) and the [experimental protocol](docs/plugin-protocol.md). Temperatures/GPU, markets/FPS, dynamic settings and physical live refresh remain pending. Other languages may implement the process protocol. Host-only checks need no Rust; provider/release checks use the pinned toolchain and Microsoft C++ x64 build tools.

## Change expectations

Keep changes focused and explain the concrete before/after behavior. Preserve profile compatibility and update parser, schema, editor and documentation together when public fields change. Add tests for meaningful boundaries: invalid/stale input, failure, cancellation, resource ownership and compatibility. Do not introduce dependencies or general frameworks without a current need.

Collection, dependencies, rendering and scheduler changes need [resource measurements](docs/performance.md), including external collectors. Record missing evidence honestly; do not claim a language choice establishes low memory use or gaming performance. Do not collect live metrics or exercise real USB/shortcuts during automated tests.

Before submitting, state the validation performed, any compatibility consequences and remaining physical limitations. Documentation-only changes need link/status/contract checks. Keep source/release exports complete and free of personal profiles, identifiers, credentials, caches and logs; retain dependency licenses/notices.

## Device and setup boundaries

Keep direct HID operations explicit. The default command must only inspect; automated checks must never open a device. Preserve the Studio-running check, single-controller guard and exact report-capability selection. Do not silently add firmware commands, drivers, background startup, network access or keyboard injection.

In the shown app, saved hardware assignments automatically start trusted providers. Metadata discovery alone must not execute them. Preserve bounded I/O, serialized replacement and stop/exit cleanup. Device ownership remains with the host. New collector/driver-dependent setup, elevation, services, Windows startup and security changes are separate decisions. Read [stability](docs/stability.md) before device work; [architecture](docs/architecture.md) describes the future live-refresh gate.

## Physical reports

For a hardware report, include the app version, Windows version, device model and firmware version if known, command used and observed result. Inspect output includes a USB device path; redact device-specific identifiers before posting it publicly. Do not include personal crash dumps or unrelated computer logs.

Distinguish successful compilation and protocol checks from verified physical controls, actual display changes and long-term reliability. Zero decoded events does not establish an input test passed. Identify any untested hardware behavior in your change description.
