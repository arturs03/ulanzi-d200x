# Plugin delivery and open-source plan

Prepared 2026-10-05. Status: preview 11 implements CPU/RAM preview plus one shared selected temperature-log adapter, a source picker/private settings, fixed-source discovery/supervision, schemas, packaging and CI configuration. Real-temperature verification, later sources/generic settings, stable compatibility, publication and bridge retirement remain pending. The [live-widget plan](live-widgets-plan.md) tracks the ten values; [provider instructions](providers.md) describe implemented behavior and the [protocol](plugin-protocol.md) is experimental.

## Product and migration direction

Recommended scope: develop D200X Direct and its providers in the existing Direct repository; retain the sibling `plugins_hardware` project as a migration reference. Ongoing Studio support is a separate product-scope choice. A Direct provider is not a Studio plugin and cannot be installed in Studio.

The older implementation has three distinct parts:

| Part | Actual role | Proposed treatment for Direct |
| --- | --- | --- |
| TypeScript/HTML Studio plugin | Runs inside Studio, fetches sensor snapshots and renders using the Studio SDK | Replace with the C# widget/editor layer; do not bundle its browser or SDK |
| Node.js / esbuild | Builds the Studio JavaScript; no Node sensor server is implemented | No Node/npm dependency in the planned Direct host/provider build or runtime |
| C# `sensor-bridge` | ASP.NET loopback HTTP server; LHM GPU polling and CPU-only OHM CSV adapter | Replace with supervised Rust system provider using Windows APIs and a fresh selected CSV |
| Existing external monitor | Performs underlying sensor collection and produces the CSV | Keep as an optional source requirement for temperature/GPU metrics; no automatic installation or removal |

Replacing the bridge removes its HTTP listener and duplicate application layer from the Direct path. It does not produce sensor data by itself or remove the external monitor's hardware access/resource cost. CPU/RAM usage works without that monitor; CPU/GPU temperatures, hotspot and GPU load remain conditional on a supported fresh source. Do not claim all monitoring is dependency-free.

The sibling is currently a folder outside the Direct Git repository, with no top-level source license found during inspection. Treat its code/tests as reference material until provenance and redistribution rights are established. Direct already contains an MIT license. Preserve third-party notices rather than applying Direct's license to upstream code. The official [OHM logger source](https://github.com/openhardwaremonitor/openhardwaremonitor/blob/master/Utilities/Logger.cs) documents the daily CSV and carries an MPL-2.0 notice; [LHM's license](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/master/LICENSE) must be inventoried if its code/binaries are ever redistributed. Record the exact pinned revisions/licenses for shipped dependencies, rather than relying on these moving reference URLs.

## What a plugin contains

The first extension contract is a **read-only data provider**. A provider supplies numbers, units, source IDs, observation timestamps and availability. The host supplies widget formatting, icons, actions, input handling and device writes. Providers share acquisition across keys and return the union of requested values.

| Provider | Values | Collection and dependencies |
| --- | --- | --- |
| System | CPU/RAM usage; CPU package, GPU core/hotspot/load when available | Windows APIs plus one shared bounded read of an explicitly selected OHM-format CSV; no LHM/HTTP bridge in the initial Direct implementation |
| Markets | SOL-USD, NVDA, AAPL | Separate documented exchange/stock adapters in one process; source-specific quotas, cache and per-user credentials |
| FPS (optional later) | Selected game's defined frame-rate metric | Supported frame collector or explicit capture input; collector setup/overhead remains a separate gate |
| Fixture (development only) | Clearly identified test values and deliberate failure modes | Hardware-free protocol/lifecycle checks; excluded from normal release installation |

Use one persistent process per enabled provider, not one process per tile. System collection begins around one second; market requests follow their source's cadence. Profiles reference stable provider/metric/source IDs and presentation only. Log paths, hardware/process selections and credentials live in per-user settings with a defined source-reference mapping. A shared profile must ask the recipient to resolve unavailable local sources rather than silently selecting another sensor.

Existing actions remain compiled `IActionModule` implementations; hotkeys, website opening and application launching need no data provider. Community action contributions initially require a host build. A downloaded action-plugin API would require its own design and is outside this read-only contract. This prevents an ambiguous promise that the first plugin API supports every integration.

## Source and package layout

Keep the host, bundled provider source, protocol and conformance tools together initially so protocol changes can be reviewed and built in one checkout. Community providers can have independent repositories. Split first-party providers later only when release ownership or compatibility needs justify it.

Implemented source layout (markets/FPS/example templates remain planned):

```text
ProviderHost/                      C# discovery, supervisor and value store
providers/
  Cargo.toml                       shared Rust workspace
  Cargo.lock
  rust-toolchain.toml               chosen pinned toolchain
  crates/
    protocol/                      framing/types/lifecycle helpers
    fixture/                       test executable
    system/                        Windows usage + selected CSV adapter
    markets/                       stock and exchange adapters
    fps/                           deferred optional adapter
schemas/
  plugin-manifest.schema.json
  provider-wire.schema.json
examples/providers/                minimal Rust example and synthetic fixtures
docs/                              authoring, source setup, compatibility
```

Name public provider IDs/namespaces once in P1; keep metric IDs stable. Do not encode executable versions or a developer's sensor IDs in portable bindings. Package version, manifest version, wire version and profile schema version remain separate.

Each installed provider folder contains `plugin.json`, its Windows x64 executable, required resources, its license, third-party notices and source/release metadata. Manifest fields and source/settings enumeration must be finalized in P1; this plan adds no working JSON fields. Install under an app-managed per-user plugins location, separate from the app payload so app updates/uninstall do not erase community providers or settings. Finalize that preservation policy with the installer changes.

The app installer includes the system provider; it runs automatically only for saved hardware assignments in the shown app. Markets/FPS can ship as optional separate provider archives. Discovery itself is metadata-only; profile/binding/source changes drive bounded supervised execution. Main/tray Stop pauses collection. No HTTP listener, npm setup, downloaded DLL loader, shell hook or install script is part of the initial contract. Process separation is fault containment; these executables still run with the user's permissions.

## Contributor and user workflows

**Host contributor:** use the existing .NET build/checks. **Provider contributor:** install the chosen Rust toolchain once, build the locked workspace, run protocol and adapter fixtures, then package the release executable. When providers exist, update the build scripts so host-only work can run without Rust and provider/release work explicitly requires it. End users install prebuilt packages and need neither a compiler nor Node.js.

**Community author:** start from a documented minimal provider; implement hello/snapshot/shutdown plus finalized source/settings discovery; keep stdout for the protocol and stderr bounded/redacted. Test with a hardware-free host conformance command, then publish source, a versioned executable package, checksum, license, declared dependencies and supported host/protocol versions. The shared Rust crate is a convenience; conformance fixtures/schemas define a language-independent contract. No automatic package downloads or marketplace is needed for the first release.

**User:** add a trusted local provider folder through validated discovery, inspect its identity/source/dependencies, select a source and save hardware bindings in the editor. App values collect automatically; show missing setup/freshness errors clearly. Physical live-screen refresh remains gated by [transport evidence](live-widgets-plan.md#p5-live-screens-on-the-deck).

On update, disable/stop the provider first, validate its replacement and retain the last valid version/settings for explicit rollback. Do not run plugin-defined migration code. Profile validation/backup precedes migration, and unknown/removed metrics become visibly unavailable. Define supported protocol versions and host-version requirements before releasing compatibility promises.

## Open-source release work

Direct has a Git repository and a configured GitHub remote. This inspection does not establish whether the remote is public, current or release-ready. Publishing is a later concrete action; this planning work changes local documents only.

- [ ] Retain MIT for Direct's own source and use it for new first-party provider source under project ownership. Establish provenance/license before copying sibling code/assets/tests; record upstream licenses separately. Do not relicense vendored SDKs or collector dependencies.
- [ ] Add provider authoring/setup/compatibility docs, a minimal example and synthetic test data. Document which sensor formats/hardware/collector versions were actually tested and what remains optional.
- [ ] Add Windows CI for the existing .NET checks and, once present, locked Rust fmt/clippy/tests/release builds and host conformance fixtures. CI must use no real USB, live market keys, sensor monitor or game capture.
- [x] Extend `package.ps1` source export to include `ProviderHost`, Rust source/manifests/lockfile/toolchain pin, schemas, package examples and CI configuration. Provider packages include dependency license files; source excludes `target`, `bin`, `obj`, caches, private settings and generated runtime payloads.
- [ ] Build installer/provider archives and source archive from the same identified revision. Include versions, exact dependency notices and SHA-256 checksums; verify archive contents and a clean-checkout/source-archive build. Checksums support integrity checks; document whether packages are signed.
- [ ] Review the complete candidate publication contents, including Git history if publishing existing history, for personal paths, sensor/device identifiers, logs, credentials and proprietary artwork. The sibling README/AGENTS contain machine-specific paths/hardware context and must not be copied wholesale into public docs.
- [ ] Provide contribution guidance and a private vulnerability-report route, with actual maintainer contact decided before publishing it. List supported versions and experimental features without implying long-term stability.
- [ ] Release the host/system preview independently when its gates pass. Market/FPS packages and physical live screens can follow separately; an incomplete ten-value deck must be described explicitly. Full deck completion still uses the live-widget plan's acceptance criteria.

## Retiring the older bridge

Retirement means removing it from the intended Direct runtime/build dependency path first. Deleting the older source, installed Studio plugin or saved Studio configuration is not part of this plan update.

1. Record the old metric/source semantics and translate relevant edge cases into synthetic Direct fixtures after provenance review. Reimplement bounded parsing and preserve original source observation times; do not carry over the old bridge's snapshot timestamp as the sensor timestamp.
2. Deliver CPU/RAM preview plus fresh CPU temperature, then separately verify the selected GPU core/hotspot/load from the same CSV. Old successful LHM GPU polling does not prove that the CSV replacement currently provides them.
3. Compare values, stale behavior, repeated start/stop cleanup and total host/provider/monitor overhead. Require the replacement to work without the old HTTP endpoint; an offline bridge alone is not migration proof.
4. Once the desired readings pass, record the old bridge as unnecessary for Direct. If a field cannot migrate, keep it explicitly unavailable and decide on a separate optional source; do not add an invisible legacy fallback.
5. After the product-scope decision, mark Studio work as retained reference or separately maintained. Any later installed-component cleanup uses an exact inventory and preserves the user's external monitor/profiles; source archival/deletion is a separate explicit task.

## Next implementation slice

Complete the P0 source/transport research in parallel with P1 foundation work where independent; this is dependency planning, not agent delegation. Start coding with a shared protocol/manifest fixture, C# supervisor and Rust fixture provider, then add typed bindings and CPU/RAM preview. Add CPU temperature once a fresh selected source is available. The fixture stage must not wait for a running monitor, market subscription or USB transport proof.

Before implementation, resolve: ongoing Studio support; chosen Rust toolchain/setup; fresh sensor selection; stock entitlement; optional FPS collector setup; and the physical update primitive. Only the choices required by a slice block that slice. Numeric performance budgets follow baseline measurements, rather than an assumed benefit from replacing Node or C#.
