# Architecture and established decisions

Status: accepted development direction on 2026-10-05; preview 11 adds selected CPU/GPU/hotspot log temperatures to the executable provider and inline content editor. Private settings are delivered at hello, with one shared bounded log read per snapshot. The protocol remains experimental; real-temperature/resource verification, later sources/generic settings/live USB refresh remain pending. See [implemented provider behavior](providers.md).

## Product scope

D200X Direct is an independent, open-source Windows controller for the Ulanzi D200X. Users edit declarative profiles, assign actions and eventually select optional live values. The app should be inexpensive to run during games and normal desktop work. Stability, explicit control and truthful readings take precedence over feature count.

The project currently targets Windows x64 and .NET 8. Other devices/platforms are unverified. Official Ulanzi Studio plugins are a different integration; do not promise that a Direct provider folder can be loaded by Studio.

## Accepted language and extension decisions

- Retain C# for the WinForms UI, profiles, rendering, Windows actions, USB control and per-user installer. Do not rewrite established behavior merely to unify languages.
- Prefer Rust for bundled data providers, with small dependency sets and measured resource use. Rust is not a performance guarantee or a replacement for hardware validation.
- Use separately launched executables and a versioned JSON protocol for providers. Community implementations may use Go or another language. Avoid loading provider DLLs into the host or introducing a native Rust/Go ABI contract.
- Discover provider folders as metadata without execution. The shown app automatically runs trusted providers for saved hardware bindings on startup, save, profile import/reload and source changes, per the user's preview 12 preference. Unsaved key selection and standalone profile parsing remain non-executing.
- Keep data providers separate from action modules. The initial provider API supplies read-only values, never keyboard input, arbitrary commands or a device writer.

## Current source map

| Source | Responsibility |
| --- | --- |
| `Profiles.cs`, `ProfileEditing.cs`, `IconReferences.cs` | Strict configuration, validation, immutable edits and icon references |
| `Actions.cs`, `ActionPresets.cs` | Compiled action catalog, descriptors, validation and dispatch |
| `HidDevice.cs`, `Protocol.cs`, `DeviceSession.cs` | Native HID selection, framing/transport, cancellation and paired-session lifetime |
| `Desktop/MainForm*.cs`, `Desktop/ActionRunner.cs` | WinForms state and Windows action adapter |
| `Desktop/ProfileImages.cs`, `Desktop/IconStore.cs`, `Desktop/Theme.cs` | CPU/GDI images, bounded icon imports and presentation |
| `SelfTests.cs`, desktop check classes | Hardware-free protocol/configuration checks and isolated UI checks |
| `Installer/`, `build.ps1`, `package.ps1` | Per-user setup, build and source/runtime release export |
| `ProviderHost/`, `WidgetBindings.cs` | Metadata validation, bounded process supervision/freshness, declarative preview bindings |
| `providers/`, `build-providers.ps1` | Pinned Rust protocol, fixture/system executables and licensed packages |
| `Desktop/MainForm.KeyEditor.cs`, `Desktop/MainForm.Widgets.cs` | Inline content/action assignment, automatic assigned-provider sessions and changed-value tile details |

Preview 12 collects saved hardware content automatically, independently of available samples or device control. Shortcut/link/application types specify a press action; hardware data and label/image content need none. One owned process per assigned provider serves shared demand; serialized replacement coalesces rapid edits and joins old sessions. Stop pauses collection; no failed-provider restart loop is added. System data supplies CPU/RAM and selected log temperatures; GPU load, markets/FPS and physical live refresh remain pending. Enable actions and Keep screen on default on, but device input still requires Start and page transfer still requires Update screens. No sensor bridge is bundled with Direct. The sibling Studio project remains reference material, not a dependency.

The [plugin delivery and open-source plan](plugin-delivery-plan.md) proposes keeping bundled provider source and conformance tools in this repository, with system/markets/optional FPS package boundaries. The older Studio plugin uses Node only for its JavaScript build and a separate C# HTTP sensor bridge at runtime. The proposed Direct system provider replaces that bridge with Windows usage APIs and a fresh-log adapter; external sensor collection remains a source requirement. Retaining the sibling as reference while verifying replacement readings does not authorize deleting it or claim ongoing Studio support has been agreed.

## Intended data path

```text
Provider folder -> manifest validation -> saved hardware assignments in shown app
                                            |
                                     supervised process
                                            |
                               bounded JSON over stdin/stdout
                                            |
                           host validation + freshness + latest values
                                            |
                              widget formatting + image cache
                                            |
                           bounded, serialized device update scheduler
                                            |
                                         D200X

Physical input -> action enablement -> existing action catalog -> Windows
```

The paths share application/session lifetime but retain separate permissions, queues and errors. A provider value must never become an action command.

## Ownership and failure rules

| Owner | Owns | Does not receive |
| --- | --- | --- |
| Host/session | Device guards, HID handles, ordered writes, cancellation | Provider-controlled USB commands |
| Provider supervisor | Verified executable path, child lifetime, bounded IPC, health | Shell command strings or automatic privilege escalation |
| Provider | Its selected source, bounded cache and typed samples | Host HID handles or a privileged action interface |
| Widget layer | Binding, units, precision, freshness presentation | Direct hardware polling or process creation |
| Action layer | Validated actions following physical events and enablement | Provider-originated action requests |

Stop, exit, disconnect and disable must have defined cancellation and cleanup behavior. Failed/hung providers become unavailable independently; report the failure, stop the affected process and require explicit re-enable. No automatic restart storms. Device I/O failure stops device control; do not retry or reconnect automatically.

One provider can serve several related values. CPU/GPU/hotspot tiles should share a collector when their source permits it; market symbols share a cache. Provider instances are not created per key or per refresh.

Process separation helps contain faults but is not a security sandbox. Executables run with the user's account permissions. Manifests describe requested capabilities; declarations alone do not enforce file or network restrictions. Load trusted code only and document this in the plugin UI. No custom drivers, firmware changes, elevation, services, startup registration or Windows security changes belong in ordinary provider installation.

## Data-source decisions

- CPU/RAM usage can use read-only Windows interfaces. Keep acquisition out of UI callbacks.
- CPU temperature may reuse fresh logs from an already-running supported monitor. Reading a CSV does not remove that monitor's underlying hardware access or resource cost.
- GPU core, hotspot and utilization require a suitable source and explicit adapter selection. Never substitute core for hotspot or silently switch to an integrated GPU. New GPU polling/driver-dependent setup is a separate, explicit integration decision.
- FPS requires actual frame-timing data for a selected process. Do not use monitor refresh rate as game FPS. PresentMon or another collector is optional; investigate privileges, compatibility and total overhead before integration.
- Markets need documented providers, explicit symbol/currency, source timestamps and data entitlement. Honor provider quotas; do not infer an API from a website link or ship a shared secret.
- Missing, invalid, ambiguous, stale or offline data is unavailable. A cached quote may be displayed only with honest delayed/market-close/stale context. Never invent a reading.

## Device refresh remains a verification gate

Preview 14 uploads an entire page explicitly while stopped, including a snapshot of assigned values. It captures samples on the UI thread, renders before writes and leaves runtime values out of the profile. Keep screen on sends small mode/time packets; it is not a live-widget transport. The bounded one-shot Test selected screen probes documented D200 command 0x000d with exactly one saved LCD key and temporary TEST label. D200X applicability remains an inference requiring the [physical procedure](display-transport.md).

Continuous USB refresh stays disabled until a bounded update primitive is physically verified. Do not use repeated full ZIP uploads to simulate a live display. Future screen refresh needs a separate explicit user opt-in, one write scheduler shared with keep-screen-on work, coalescing of obsolete frames and stop-on-error behavior. Existing display commands are normal device operations, not firmware flashing.

## Implementation sequence

For concrete work packages covering the current Studio draft's eight placeholder tiles, use [the live-widget implementation plan](live-widgets-plan.md). It expands this sequence and records source/transport gates without changing the accepted ownership rules.

1. Finalize manifest/protocol validation with a hardware-free fixture provider, supervisor and malformed/slow/exited-process tests.
2. Add editor/preview bindings and fresh CPU-log plus CPU/RAM sources. Preserve existing v1 profiles; extend runtime validation and schema together.
3. Add market adapters with secure credential references, entitlement/freshness UI and bounded caching.
4. Verify device refresh physically, then expose opt-in live screens with conservative scheduling.
5. Add optional GPU/FPS integrations after source/setup decisions and resource/lifecycle checks.

Do not mark a stage complete from an interface mockup or sample output. Record implementation, automated results and physical results separately. New architectural choices should update this document with their reason and compatibility consequences; small local changes do not need ceremony.

Related: [protocol draft](plugin-protocol.md), [engineering](engineering.md), [performance](performance.md), [stability](stability.md), [physical evidence](hardware-validation.md).
