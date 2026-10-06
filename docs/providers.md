# Building and using data providers

Preview 12 automatically collects saved hardware assignments and removes the separate Start data/Stop data controls. Preview 11's shared monitor-log reader supplies selected CPU package, GPU core and GPU hotspot temperatures alongside CPU/RAM usage. The API is experimental. Generic dynamic source/settings enumeration, protected credentials, markets/FPS, archive installation/update UI and physical live refresh remain pending.

## Try the app preview

The installer bundles **System data**; saved assignments drive collection. Click a screen key, choose **Hardware data** under Key type, then select CPU/RAM usage, GPU load, CPU temperature, GPU temperature or GPU hotspot temperature. Wide key 13 has two additional optional value choices. **When pressed (optional)** defaults to No action; add a shortcut, website or application only if wanted. Save changes assigns the content and action in one validated edit and backs up the previous profile. Assignment needs no provider execution or live reading; all six system choices remain selectable even if the provider is missing. Existing missing-provider bindings and custom label/precision are retained when editing.

Saving Hardware data, opening the app with saved bindings, loading/importing a valid profile or saving a sensor-source change automatically collects assigned values. One process per assigned available provider supplies all its selected keys; unused providers/metrics are not started/queried. Rapid changes coalesce, and old collectors exit before replacements launch. CPU is initially unavailable until two Windows counter snapshots exist; RAM uses physical total/available bytes. Collection is roughly once per second, respecting selected metrics' minimum intervals (this slice supports up to five seconds). Machines with more than 64 logical processors show CPU unavailable until processor-group support is implemented.

Values update in the app's tile detail. Preview 14's **Update screens** resumes assigned data and sends a snapshot of current values with labels/icons; the uploaded numbers remain static afterward. Main/tray **Stop** pauses hardware data and device control. **Start**, **Update screens**, profile reload or a saved binding/source change resumes it. Removing all bindings and app exit end collection. Data may run separately while device control is stopped. Appearance/action edits without binding changes keep the session. Failed providers stay unavailable without restart loops; reload or update an assignment/source to retry. Failures do not stop other providers or controller input. Missing/stale values show `--` independently. Continuous physical refresh requires the [one-shot transport verification](display-transport.md).

Copy trusted community provider folders into `%APPDATA%\D200XDirect\plugins`, then reopen the app or reload the profile. Automatic collection rescans metadata before execution; saved assignments can therefore execute these trusted providers when the app opens. Bundled providers are read from the app's `plugins` folder. Duplicate IDs across these locations are rejected. Discovery itself performs no launch. The editor lists each fixed metric/source pair from metadata (up to 256 choices); existing assignments beyond that list remain editable. Fixture providers are excluded in normal use. Generic dynamic source/settings selection remains a future contract; the bundled system provider has the specific temperature setup described below. Providers run with the user's Windows account permissions; capability declarations and process separation are not a sandbox.

To remove data, select a different Key type and save. Shortcut, Website / link and Application set the corresponding press action; Label / image is display-only. Saving the Hardware data type without a first value, or with duplicate values on key 13, is rejected without changing the profile.

## Configure temperatures

Preview 16 adds **GPU load** (GPU Core load, percent) using the same reader. The source picker has a fourth field with exact `/load/` identifiers. When GPU load is assigned and an existing source has no load selection, the collector reads bounded header metadata once, matches only one GPU Core load on the already configured GPU, and saves that private selection. Multiple matching sensors remain unconfigured. GPU memory load is never substituted. Old source settings continue to load; update the host and bundled provider together before writing the new optional `gpuUsage` setting.

The monitor must actually be running and writing today's CSV at least once every five seconds. Preview 17 shows this reminder directly below Hardware data in the wider mapping panel when a monitor-backed temperature or GPU load is selected. CPU/RAM usage alone needs no external monitor. Saving source settings only identifies sensors; it does not start a monitor or enable logging. Activity reports a missing current-day log, stale rows, invalid sensors and recovery, without repeating unchanged status on every poll. CPU/RAM values remain independent of log failure. Keep screen on only sends periodic keep-awake commands; it does not refresh displayed numbers.

If CPU load works but CPU temperature is missing, check whether the monitor itself exposes a CPU temperature sensor and includes it in today's log. Task Manager CPU usage is not a temperature source. A monitor can remain open and supply GPU readings while its CPU driver is blocked; changing a label or choosing an old CSV header cannot restore a missing live reading. Selecting another collector or updating its driver is a separate setup decision.

Select a Hardware data key and choose **Sensor source…**. In preview 15, browsing to an existing Open Hardware Monitor CSV log folder reads its names automatically. A unique CPU Package and unique GPU Core / GPU Hot Spot on one GPU are preselected. Multiple GPUs or duplicate sensor roles require a manual choice; GPU Memory is never substituted for hotspot. Each choice shows its exact identifier. Opening a saved source reads its names automatically and preserves exact saved selections, including intentionally unconfigured roles; a missing saved sensor is left unconfigured rather than replaced. **Read sensors** remains available after manually entering a folder or to retry. Review and **Save source** to replace collection automatically. The app does not install, start or configure a monitor.

Settings live in `%APPDATA%\D200XDirect\system-sensors.json`, outside portable profiles. Bindings use source alias `monitor.local`; recipients select their own source. Source settings are sent in the system provider's hello request, never in command-line arguments, returned samples or diagnostics. The Studio layout draft assigns keys 10/11/12 to the three temperatures and key 13 to CPU/RAM/GPU usage; loading it starts assigned data in the shown app but does not change USB screens.

The supported format has an identifier header, a quoted name header, invariant numeric columns and local `MM/dd/yyyy HH:mm:ss` timestamps. The folder must be local, without links. Header selection can use historical names while stopped (at most 367 files inspected); actual acquisition reads only today's `OpenHardwareMonitorLog-yyyy-MM-dd.csv`. All selected temperatures share a maximum 64 KiB prefix and 64 KiB tail read, followed by a bounded header consistency check (at most 192 KiB total), once per snapshot. The last complete row supplies each value independently; incomplete append bytes are ignored. No previous row is substituted for a missing value. Duplicate columns, invalid numbers/ranges, missing sensors or wrong names show unavailable.

CSV timestamps must use this computer's Windows time zone. Its standard/daylight offsets are round-tripped through Windows clock conversion; ambiguous/repeated and nonexistent times are unavailable. Changing the Windows time zone requires selecting the source again. Preserve the converted source observation time; accept at most five seconds old or two seconds ahead. Daily rollover chooses only the same configured folder's current-day file. Never fall back to yesterday's values or GPU core for hotspot. The currently inspected historical log is stale, so real-temperature verification remains pending. Other monitor formats and remote-machine logs are unsupported.

## Build and check

Host-only development still needs just the .NET 8 SDK. Provider/release builds additionally need the Microsoft C++ x64 build tools and the Rust toolchain pinned in `providers/rust-toolchain.toml`. A repository-local toolchain under ignored `.tools/cargo` and `.tools/rustup` is supported; neither it nor build caches are exported as source. Automated checks request synthetic fixture values and synthetic log temperatures only, without real Windows usage or monitor readings.

```powershell
./build-providers.ps1
./build.ps1
dotnet out/D200xDirectController.dll provider-check out/provider-packages/fixture
dotnet out/D200xDirectController.dll sensor-log-check out/provider-packages/system
dotnet Desktop/bin/Release/net8.0-windows/D200xDirect.dll --ui-check out/provider-packages/fixture

# Separate, deliberate Windows-usage integration check (no USB):
dotnet out/D200xDirectController.dll provider-preview out/provider-packages/system 3

# Build the installer/source/checksums, including the assignment-driven system provider:
./package.ps1
```

`provider-preview` is an explicit execution command, not a discovery operation. It requests all metrics at their declared interval (up to five seconds), takes 1–60 snapshots and stops the child. `provider-check` accepts only the purpose-built fixture package, tests failure/cancellation/descendant cleanup and is not a general third-party conformance certification yet. The optional fixture argument to `--ui-check` verifies the inline data lifecycle using synthetic values; ordinary UI checks launch no provider. Both use isolated profiles and never access USB or run key actions.

Packages are built under `out/provider-packages`; only System data is copied to `out/runtime-plugins` for desktop builds and packaging. The fixture remains a test artifact. Per-package licenses and the exact Cargo.lock dependency inventory/notices accompany generated packages. `package.ps1` exports C#/Rust source, the locked dependencies/toolchain specification, schemas, examples in package metadata, docs and CI configuration; it excludes build caches and personal settings. Published CI has not run until this change is pushed.

## Author an experimental provider

Use `providers/crates/protocol` and the system/fixture crates as examples, or implement the same [wire contract](plugin-protocol.md) in another language. The protocol crate handles bounded frames, request validation, hello/shutdown/EOF and response serialization; adapters implement snapshot acquisition. Stdout is JSON only. Diagnostics are drained without retaining raw provider text in the host.

Start from `providers/packages/system/plugin.json` and the [manifest schema](../schemas/plugin-manifest.schema.json). Each metric declares typed units, minimum interval and fixed source IDs. Put `plugin.json`, the named `.exe`, license/notices and required resources together in one local folder. Windows x64 is the only implemented platform; do not embed a shell command or absolute executable path. The host rejects linked path components and mismatched identities/versions.

Current requests/responses use experimental protocol version 1 and monotonically increasing positive request IDs. Snapshot requests carry a `selections` array; responses must contain exactly one sample per requested metric/source pair. Successful samples require a finite in-range number and original UTC `observedAt`; unavailable/error samples require null value/time. See the [envelope schema](../schemas/provider-wire.schema.json) and runtime rules in the protocol document. The schema validates envelopes; it does not alone certify lifecycle, source selection or freshness behavior.

Build/package versions and protocol/profile versions are separate. Keep IDs stable. Provider installation/update/rollback automation and a supported compatibility policy are not implemented; treat this as a contributor preview. Temperature adapters must preserve CSV observation time, not copy the old HTTP bridge's snapshot timestamp. Do not assume the sibling Studio project is available or licensed for reuse.
