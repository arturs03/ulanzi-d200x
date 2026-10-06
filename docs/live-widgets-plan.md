# Implementation plan: real values on the current deck

Prepared 2026-10-05 against the Studio layout draft and the locally saved active profile. Status: preview 16 adds GPU Core load to the CPU package/GPU core/GPU hotspot log reader, source picker and private hello settings. Synthetic parser, DST, CSV/IPC, independent-value and source-editor checks pass. Real temperature/load/resource verification, markets/FPS, generic settings enumeration and physical live refresh remain pending. The public Studio draft binds temperature keys 10/11/12 and usage key 13; the user's active profile was explicitly mapped on request, with private backups. The monitor is not running and the latest inspected log remains historical, so mapping is not proof of current readings. Follow [architecture](architecture.md), [experimental protocol](plugin-protocol.md), [engineering](engineering.md), [performance](performance.md) and [provider usage](providers.md). The [delivery plan](plugin-delivery-plan.md) tracks packages, public release work and older-bridge migration.

The accepted editor flow is **select a physical screen cell → choose Key type → specify content → optionally configure a press action → save**. Hardware data and label/image content can be display-only, and assignment does not require a live reading. Preview 12 automatically collects saved/loaded assignments, replaces collection after data/source changes and removes the separate Start data and AI guide controls. Keep screen on and Enable actions default on. Main/tray Stop pauses collection; failed providers do not restart in a loop. Device Start and page upload remain explicit. This UX completion does not establish physical live refresh.

## Plugin delivery and older project

The older `plugins_hardware` project uses Node.js/esbuild only to build its HTML/TypeScript Studio plugin. Its hardware reader is a separate C#/.NET HTTP bridge using LibreHardwareMonitor for GPUs and an OHM CSV adapter for CPU temperature. Direct does not depend on that sibling today.

Proposed delivery: keep the C# host; build one shared Rust system provider, one markets provider and an optional later FPS provider. The system provider replaces the old bridge in the Direct path with Windows CPU/RAM APIs and selected fresh sensor logs. Node/npm, the Studio SDK and the HTTP bridge are not required for this planned Direct implementation. The external monitor remains a source dependency for sensor readings; replacing the bridge does not replace hardware collection.

Keep the older source as a migration reference while replacement readings are verified. Recommended public scope is Direct with bundled provider source in the same repository; continuing Studio support remains a product-scope choice. Runtime removal, source deletion and repository publication are not performed by this plan. Providers supply read-only data; actions stay in the existing compiled action API, and the host renders/widgets/writes USB.

## Scope and current evidence

Eight LCD tiles contain ten missing values. Keep their positions, icons and existing press actions. A price widget must continue opening its assigned website; a live display binding is independent of its action.

| Key | Current tile | Values to implement | First source/approach |
| --- | --- | --- | --- |
| 5 | NVDA | NVDA price in USD | Documented stock API with stated data age/entitlement |
| 6 | SOL-USD | Solana price in USD | Documented exchange ticker, named exchange and trade timestamp |
| 7 | AAPL | AAPL price in USD | Same stock adapter/cache as NVDA |
| 9 | FPS | Selected game's frame rate | Optional PresentMon adapter, with defined frame-rate semantics |
| 10 | CPU temperature | CPU package temperature | Fresh CSV from an already-running supported monitor |
| 11 | GPU temperature | Selected GPU's core temperature | Same monitor CSV, explicitly selected sensor |
| 12 | HOTSPOT | Same GPU's hotspot temperature | Same monitor CSV; never substitute core temperature |
| 13 | CPU / RAM / GPU usage | Three independent percentages | Windows CPU/RAM APIs plus selected GPU load from the monitor CSV |

Inspection on 2026-10-05 confirmed static labels and no widget bindings in the active profile. The existing loopback bridge health endpoint did not respond. The latest existing monitor CSV was dated 2026-10-04: its headers contain CPU package, GPU core, GPU hot spot and GPU core-load columns. These are historical source-format observations, not live readings or proof of current GPU identity. The earlier C# CSV adapter imports CPU temperature only; a Rust reader must deliberately add the other selected types and validate them.

Rust/Cargo were not found on PATH during planning. Rust build-toolchain setup is a development prerequisite for the first provider, not an end-user requirement. No monitor, driver, service or tracing collector was started or installed during this inspection. Do not copy local paths/sensor IDs into the public template.

## Work packages and dependencies

Preview 11 completes the temperature adapter/editor implementation slice; preview 16 adds GPU Core load (`gpu.usage`, percent) to that same bounded reader. All four share alias `monitor.local`; exact local selections stay in private settings and GPU sources share one hardware identity. Synthetic checks cover independent missing values, exact headers, temperature/load ranges, complete bounded tails, source time/DST, stale/future data, today's-file selection and host IPC/cleanup. P0/P2/P3 exit gates remain open: no fresh real log, measured monitor/desktop overhead or physical temperature/load update has been verified. Keep the older bridge/source until real-source migration is demonstrated.

| Package | Deliverable | Depends on | Exit evidence |
| --- | --- | --- | --- |
| P0 | Resolve sources and investigate a bounded display update | Existing source/protocol evidence | Chosen source semantics; documented transport candidate/limitations |
| P1 | C# provider host + Rust fixture/SDK foundation | Draft protocol review | Conformance, failure/cancellation and old-profile checks |
| P2 | Widget bindings, preview, CPU/RAM and CPU temperature | P1 | Three real values in preview, stale handling, resource results |
| P3 | GPU temperature/hotspot/usage from the same log | P2 + fresh selected source | Verified adapter/sensors and separate invalidation of missing values |
| P4 | Market provider for SOL, NVDA and AAPL | P1/P2 + source/entitlement decision | Real source timestamps, quota/credential/error behavior |
| P5 | Opt-in live physical screens | P0 + P2, extended across P3/P4 | Physical update/input/flicker/lifecycle evidence |
| P6 | Optional game FPS provider | P1/P2 + collector/setup decision | Selected-game semantics, overhead and cleanup evidence |
| P7 | Package, open-source authoring examples and release validation | Gates for each shipped package/feature | Clean-checkout/source-archive build, notices, compatibility and explicit remaining limits |

Start display feasibility research in P0, before investing in all collectors. P3/P4 can be developed independently after the host and bindings are ready; this is dependency ordering, not an instruction to spawn agents. Preview delivery and physical-screen delivery must be tracked separately. Do not assign calendar estimates until the transport/source spikes resolve their uncertainties.

## P0: source and transport decisions

Preview 14 makes explicit Update screens uploads include assigned readings and adds a bounded one-shot selected-key probe for D200 command 0x000d. Synthetic rendering/framing and capture/ownership checks are implemented; [transport evidence and physical steps](display-transport.md) distinguish D200 documentation from unverified D200X behavior. Continuous refresh stays disabled. Successful ordinary-key/key-13 tests, input/flicker/cleanup/resource evidence and a shared changed-key writer scheduler are still needed for P5.

- [ ] Verify whether the user's existing monitor is producing a fresh log and select the actual CPU package/discrete GPU core/hotspot/load sensors. Do not start or configure monitoring automatically. Record selection in per-user settings, not public source.
- [ ] Establish source IDs, units, validity rules and timestamp interpretation, including local CSV timestamps, DST/timezone conversion and daily file rollover. Confirm the discrete GPU identity instead of trusting an old adapter index.
- [ ] Research existing D200X implementations for a bounded image/widget update primitive. Do not guess USB commands, expose arbitrary packets or reuse Studio SDK calls as direct-HID commands.
- [ ] Prepare hardware-free framing/size/order tests and a deliberate single-key physical test procedure. Confirm how the double-width key 13 is updated, whether image caching/filenames matter, and interaction with image mode/keep-screen-on packets.

**Gate:** continuous USB refresh stays disabled until P5 physical evidence exists. If no bounded update primitive is verified, ship preview-only values and explicit snapshots, report the limitation and revisit transport. Repeated full-page ZIP uploads are not a fallback for continuous live widgets.

## P1: reusable provider foundation

Preview 8 completes the fixed-source/supervision slice below. A supported public API still needs dynamic settings/source enumeration, broader conformance and compatibility policy.

- [ ] Finalize manifest/wire schemas, settings/source enumeration, host-owned paths, request/response types, timestamps, statuses and protocol-version compatibility. Implement the limits in the protocol draft consistently in C# and Rust.
- [x] Add a Rust workspace with a protocol crate, fixture provider and pinned build configuration/lockfile. Isolate Windows interop from safe parsing. Build release executables; end users need no compiler.
- [x] Add C# manifest discovery/validation, explicit session enablement and a supervisor using redirected stdin/stdout plus concurrently drained bounded stderr. No network listener or shell launch.
- [x] Implement hello/snapshot/shutdown, one in-flight request per provider, deadline enforcement, child ownership and cancellation/join/termination. Stop/exit and disabling the provider must end collection.
- [x] Provide a latest-value store with per-sample status/source observation time and host freshness policy. Failed providers invalidate their live values without stopping physical input/actions.
- [x] Add purpose-built fixtures for malformed/oversized/partial messages, wrong IDs/versions, stderr floods, hung/exited children, EOF and cancellation. No USB/network/real-monitor access in automated checks.
- [ ] Add a minimal provider authoring example and a hardware-free host conformance command using the same fixtures/schemas. Community languages need no Rust dependency; freeze supported protocol versions only after this contract passes.

**Done when:** a fixture folder can be discovered without execution, explicitly enabled, queried and stopped; bad fixtures remain bounded and cannot crash the host or leave owned children running. Existing profiles/checks remain valid. Publish the protocol as supported only with these checks and a documented compatibility policy.

## P2: real preview values and system provider

Preview 8 supports typed bindings, assignment/removal and CPU/RAM values. CPU temperature, monitor configuration and fresh-source comparison remain pending; P2 as a whole is not complete.

- [x] Extend `KeyConfig`, profile validation/schema, editing/import and customization docs with optional typed widget bindings. Preserve all existing v1 profiles; choose an explicit schema/version migration if the extension cannot be compatible. Do not add runtime values/secrets to profile JSON.
- [x] Support individual numeric tiles plus key 13's three-metric composite. Preserve label/icon/color/action and show per-value unavailable state: one missing GPU value must not erase valid CPU/RAM values.
- [x] Add inline type/value/fixed-source selection independent of readings, optional press behavior, atomic validated save and explicit shared Start data / Stop data. Keep existing/missing-provider bindings editable; profile load/save starts no provider or screen transfer.
- [ ] Add a concise per-source health view: disabled, source unavailable, stale, error or active. Tile unavailable markers and activity messages exist; richer health/settings UI remains pending. Refresh only the app preview until P5 verification.
- [x] Implement shared Rust CPU busy percentage from two `GetSystemTimes` snapshots and RAM physical-use percentage from `GlobalMemoryStatusEx`. First CPU sample is unavailable; guard failed APIs, zero denominators and counter resets. Machines with more than 64 logical processors remain unsupported for whole-system CPU.
- [ ] Add the bounded fresh-CSV CPU temperature adapter to that same system provider, with explicit source/settings selection.
- [ ] Read headers and a bounded tail, not the entire growing CSV. Handle quoted fields, partial concurrent appends, locked/missing files, missing selected IDs, future/stale timestamps and midnight rollover. Start with a five-second freshness window for one-second monitor logs, configurable only within reviewed bounds.
- [ ] Begin collection around one second and format before change detection. Reuse snapshots for all selected tiles. Compare preview with the monitor/Windows values using matching sampling semantics; do not expect identical instantaneous percentages across different measurement windows.

CPU timing and memory API semantics are documented by Microsoft: [GetSystemTimes](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getsystemtimes), [GlobalMemoryStatusEx](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-globalmemorystatusex).

**Done when:** key 10 and the CPU/RAM parts of key 13 show real fresh values in the preview, stopped/stale sources become unavailable, and existing actions/profile imports retain their behavior. Record host + provider + external-monitor resource use before enabling more collectors.

## P3: all GPU values from the existing log

- [ ] Extend the same log acquisition to selected GPU core temperature, hotspot temperature and GPU core load. Share the file read/snapshot with CPU temperature; do not create one process or file read per tile.
- [ ] Discover/select exact sensor IDs with explicit adapter confirmation and typed units/semantics. Treat names as hints for setup, not sufficient identity. Core-load percentage is the selected monitor's reported metric; document that it may differ from Task Manager engine aggregation.
- [ ] Validate each sample independently and preserve missing/error distinction. Adapter changes, missing hotspot and stopped logs show `--`; never switch GPU silently or replace hotspot with core.
- [ ] Compare all three readings against the selected monitor on a fresh source, then test unplug/session shutdown, monitor exit, rollover and stale invalidation.

**Done when:** keys 11/12 and key 13's GPU component receive fresh selected values in preview with one shared log collector. If the monitor cannot supply a required field reliably, leave that field unavailable; investigate an optional bridge/source as a separate integration/setup decision. Do not introduce direct AMD polling or new drivers as an incidental fallback.

## P4: price provider

- [ ] Implement a shared Rust markets process with separate stock and crypto adapters, bounded HTTP responses/timeouts, cache reuse and source-specific refresh/quota policies.
- [ ] For SOL-USD, investigate a documented USD exchange ticker such as Coinbase, verify the product's availability/access requirements, and show the selected exchange. Last-trade price is exchange-specific, not a universal consolidated SOL price. Preserve its trade timestamp; fetching an old trade must not make it fresh. [Ticker documentation](https://docs.cdp.coinbase.com/api-reference/exchange-api/rest-api/products/get-product-ticker).
- [ ] Select an NVDA/AAPL provider after deciding free-data-first versus intraday/live entitlement. A candidate such as Alpha Vantage supports documented quotes, but its default quote is end-of-day; real-time/15-minute-delayed access requires the relevant entitlement. Keep that distinction visible. [Quote documentation](https://www.alphavantage.co/documentation/).
- [ ] Implement per-user protected credentials and provider-specific delivery before authenticated adapters ship. Never use a bundled shared API key, put keys in profile/command-line/logs or scrape Yahoo pages to replace an API.
- [ ] Show USD, data timestamp and live/delayed/close/stale status in the inspector and concise tile presentation. Any percent change needs a specified baseline from the same source; price-only is sufficient for the first implementation.
- [ ] Start crypto around 60 seconds for snapshot use if source quotas permit. Stock cadence depends on the selected plan: daily-close data gets a daily/session-aware schedule, not minute polling. Persist a bounded non-secret cache/quota state if needed so app restarts do not exhaust quotas. Respect Retry-After/outages without hot retry loops.
- [ ] Test recorded fixtures for rate limits, market close/weekends, old/future timestamps, unexpected symbol/currency, missing fields, credential failure and host stop. Keep live API checks separate from automated fixtures.

**Done when:** keys 5/6/7 show real source values with honest freshness/entitlement and retain their existing website press actions. No automatic subscription/payment is part of implementation.

## P5: live screens on the deck

- [ ] Implement only the bounded transport candidate verified in P0. Add one scheduler for all device writes, including keep-screen-on packets, without changing conservative report selection/guards.
- [ ] Require an explicit live-screen control. Values may update in preview while physical live updates remain off. Prepare images before writes, preserve key 13's aspect ratio and avoid rewriting profile labels with samples.
- [ ] Compare formatted display content; cache/bound rendered images and coalesce old pending frames by key. Begin with at most one scheduled changed-tile batch per second if the transport supports it; measure bytes, packet count and latency rather than assuming a batch is one packet.
- [ ] Verify one tile first, then key 13, then all eligible tiles. Confirm actual screen values, ongoing physical input, observed flicker/caching behavior and interaction with keep-screen-on.
- [ ] Verify Stop/exit, disable, source failure, unplug/replug and sleep/wake. Any device I/O failure stops the guarded session; no automatic retry/reconnect. Confirm stale indicators can reach the screen while a valid device session remains active.

**Done when:** implemented values update on the physical device under explicit enablement with recorded input/lifecycle/resource evidence. A transfer-complete log or rendered preview alone is insufficient. State remaining limitations per tile.

## P6: optional FPS

- [ ] Select a supported collector/version and clarify privileges/setup before integration. PresentMon is a candidate with process selection and CSV/stdout output; do not automatically elevate, change user groups, install a service or inject a game. [Console documentation](https://github.com/GameTechDev/PresentMon/blob/main/README-ConsoleApplication.md).
- [ ] Define whether the initial metric counts application presents or displayed frames, including dropped/generated frames. Label its semantics rather than promising a match with every game's overlay.
- [ ] Implement a Rust adapter for the selected process and swap chain, bounded frame-window aggregation and stale handling. Avoid capture-all by default; process identity needs PID/lifetime handling, not only a reusable executable name.
- [ ] Prefer an explicit existing capture input when suitable. If the app owns a collector, manage and stop only its process/session. Drain output continuously and avoid unbounded per-frame disk logs or retaining all frames.
- [ ] Update the displayed aggregate around once a second while measuring continuous tracing overhead. No matching game/capture yields unavailable FPS; never use monitor Hz or a synthetic demo value.
- [ ] Test multiple processes/swap chains, malformed rows, frame gaps, game exit, PID reuse, access denial and collector stop/crash. Separately compare a reproducible game workload with/without the entire collector/provider stack.

**Done when:** key 9 shows a real, defined selected-game FPS reading, cleanly becomes unavailable on game/collector exit, and measurements establish acceptable total overhead. Compatibility is stated for tested games/API modes only.

## P7: release and completion

- [ ] Follow the [delivery plan](plugin-delivery-plan.md#open-source-release-work): retain Direct's MIT license, review sibling/upstream provenance before reuse, add fixture-only CI and verify candidate source/history for private data before publication.
- [x] Extend build/package/source export for the new Rust workspace/provider binaries, licenses, schemas/package examples and CI configuration. Generated caches are excluded; only the disabled system provider is bundled as runtime code.
- [ ] Package providers as versioned folders with executable/manifest, licenses/notices and supported host/protocol metadata. Keep host-only builds possible without Rust; release checks include clean source-archive builds, checksums, disabled installation and explicit rollback/preserved settings.
- [ ] Keep installation per user, preserve existing profiles and start stopped with actions/providers/live screens disabled. Import/migrate only after validation and backup; leave the user's active layout unchanged during automated checks.
- [ ] Run relevant .NET checks, Rust fmt/clippy/tests/release builds, protocol fixtures and isolated editor checks. Verify fresh-install/update and explicit provider discovery/enablement.
- [ ] Record CPU, private memory/working set, handles, disk/network activity, render/write counts and collector/game impact. Include repeated enable/disable cleanup and bounded behavior during outages.
- [ ] Update README, schema/customization guidance, provider setup and physical evidence with implemented/verified/pending status. Public examples contain no local paths, credentials, device IDs, private samples or proprietary icons.
- [ ] Release only verified package/feature slices and state preview-only versus physical-screen support. Markets/FPS and verified live screens may follow the first host/system preview release; they do not block publishing an honestly scoped preview.
- [ ] Verify required sensor readings without the old bridge before declaring it retired for Direct. Preserve older source and existing monitor/profiles; any installed-component cleanup is a later explicit task.

The first useful milestone is **fresh CPU temperature and CPU/RAM usage in the app preview**, delivered through the real reusable provider host. The first deck milestone adds **verified opt-in physical refresh**. Full completion means all ten values have working selected sources and their actual tile behavior has been verified; conditional source availability must be stated, not hidden by placeholders or fixture data.
