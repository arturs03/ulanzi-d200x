# Executable data-provider protocol

Status: experimental implementation, 2026-10-05, preview 11. Metadata discovery, fixed-source manifests, bounded supervision, hello/snapshot/shutdown and a Rust system provider exist. Optional typed private sensor-log settings are implemented for the bundled system provider only. This is not a stable published compatibility promise. Generic dynamic settings/source enumeration, protected credentials and market/FPS policies remain design work. See [provider usage/build instructions](providers.md).

## Installation and discovery

Use `%APPDATA%\D200XDirect\plugins` for user providers, or the app payload's `plugins` directory for bundled providers. Each provider has one folder with `plugin.json`, an executable and optional non-secret resources/licenses. Local folder copying and metadata discovery are supported; archive installation/update UI remains pending. Reopen the stopped app to refresh editor choices. Preview 9 bundles only the disabled system provider.

```text
plugins/
  hardware/
    plugin.json
    hardware.exe
  markets/
    plugin.json
    markets.exe
```

Discovery reads bounded manifest files without launching anything. Reject malformed manifests, incompatible versions and duplicate IDs visibly. Preview 12's shown app automatically runs one shared process per assigned available provider on startup, saved binding/source changes and profile import/reload. The editor lists fixed pairs without requiring samples; browsing unsaved selections and standalone parsing remain non-executing. Serial replacements join old processes before launching the latest selection. Main/tray Stop pauses collection; Start, reload or saved data/source changes resume it. Exit ends all owned work. Update stopped providers; do not replace a running executable. Generic dynamic sensor/settings selection remains pending; the system provider supports the specific temperature configuration below.

Example manifest:

```json
{
  "manifestVersion": 1,
  "id": "d200x.system",
  "name": "System usage",
  "version": "0.1.0",
  "protocolVersion": 1,
  "executable": "d200x-system.exe",
  "platform": "windows-x64",
  "capabilities": ["read-windows-usage"],
  "metrics": [
    { "id": "cpu.usage", "unit": "percent", "minimumIntervalMs": 1000, "sourceIds": ["windows.system"] },
    { "id": "ram.usage", "unit": "percent", "minimumIntervalMs": 1000, "sourceIds": ["windows.system"] }
  ]
}
```

Version numbers are independent of profile schemaVersion 1. The manifest above matches the first system package. Metric IDs are stable within a provider; a host binding also identifies its source. Units and IDs are machine-readable, while names are presentation metadata. IDs contain 1–96 lowercase letters/digits separated by single dots/hyphens, starting with a letter. Preview 8 accepts percent/celsius/usd/fps units and minimum intervals of 1,000–86,400,000 ms; its short preview UI/CLI supports intervals up to 5,000 ms. Dynamic sensor/settings metadata needs a later contract.

The current `d200x.system` package is version 0.3.0 (**System data**), with capability `read-ohm-log` in addition to `read-windows-usage`. It declares `cpu.temperature`, `gpu.temperature` and `gpu.hotspot` in celsius, plus `gpu.usage` (GPU Core load, percent), at source `monitor.local`, minimum 1,000 ms. See the authoritative [system manifest](../providers/packages/system/plugin.json).

Only its hello request can optionally include `sensorLog`: `directory` (local absolute folder, maximum 1,024 characters), `timeZoneId` (this Windows computer's zone), and selected `cpuTemperature`, `gpuTemperature`, `gpuHotspot`, `gpuUsage` exact OHM identifiers. At least one sensor is required; CPU must be a supported CPU package, GPU core/hotspot must be different temperature sensors, and GPU usage must be a GPU Core `/load/` sensor. All GPU selections must belong to the same GPU. `gpuUsage` is optional for old settings; older provider binaries reject the new field, so update host/provider together. Private settings are validated before launch, never echoed in replies and never included in portable profiles. Settings on snapshot/shutdown or any response are rejected. Other providers receive no sensor settings. This narrow extension does not implement generic provider configuration. Windows/environment changes need a stopped re-selection rather than implicit source substitution.

Validate manifest size, UTF-8, known fields, ID syntax/length, versions, metric uniqueness, supported units and interval bounds. Use a fixed local executable filename inside that provider folder; reject absolute/UNC/traversal paths and linked/reparse-point escapes. Launch the resolved executable with `UseShellExecute = false`, explicit working directory, redirected streams and fixed host-owned arguments. Never interpolate a shell command or inherit secrets unnecessarily. Capability declarations are informational, not an OS sandbox.

## Transport and lifecycle

Use a persistent child process with newline-delimited UTF-8 JSON through redirected stdin/stdout. This avoids a network listener and language-specific ABI. Stdout contains protocol messages only; bounded stderr contains diagnostics. Host drains both concurrently so full pipes cannot deadlock the child.

Implemented initial limits, exercised by hardware-free fixtures:

| Limit | Proposed default |
| --- | --- |
| Manifest or individual wire message | 64 KiB maximum, enforced before full allocation/parsing |
| JSON nesting | 16 levels maximum |
| Selected metrics per request | 64 maximum |
| Outstanding host requests per provider | One; coalesce newer demand |
| Hello and snapshot deadline | 3 seconds each |
| Graceful shutdown deadline | 2 seconds, then terminate the owned child/process tree |
| Stored diagnostics | No raw text retained; continuously drain a 4 KiB buffer and count bytes |

Manifest/metric uniqueness, UTF-8, duplicate JSON members and nesting are runtime checks in addition to the schemas. Discovery is bounded to 64 immediate provider folders per location. A snapshot must return exactly one matching sample for each of 1–64 unique selections. Percent is restricted to 0–100, celsius to -50–200, USD/FPS to nonnegative values; future UTC timestamps beyond two seconds are rejected. The source-specific validity policy may be stricter.

These remain engineering defaults, not measured performance claims or a stable compatibility promise. Longer acquisition needs a future explicit negotiated contract. Windows job ownership ends descendants, including those surviving parent exit. Launch uses no arguments and an environment allowlist; this is not a security sandbox. The supervisor expects serialized API calls. Caches apply original observation age plus monotonic elapsed age; polling the same timestamp cannot rejuvenate it.

1. Host launches a provider required by saved assignments and sends `hello`; the response identifies the provider and supported protocol. Reject identity/version mismatch. CLI execution remains an explicit command.
2. Host sends `snapshot` for the union of selected metrics/sources, no faster than the effective sampling policy. Preview 13 waits cancellably on a monotonic clock when a caller or Windows timer wakes early; the three-second response deadline starts with the exchange, after the cadence wait. Validation/wait/exchange have one owner, and overlapping requests are rejected. Cancelling before sending a request leaves a healthy provider available for owned disposal; cancellation during I/O still stops it. Provider returns one bounded response. No unsolicited values in the initial protocol.
3. Host sends `shutdown` on stop/disable/exit, closes stdin and waits within the deadline. EOF also tells the provider to stop. Host disposes streams/process handles and verifies child exit.

Snapshot request example:

```json
{"protocolVersion":1,"requestId":2,"type":"snapshot","selections":[{"metricId":"cpu.usage","sourceId":"windows.system"}]}
```

Hello response adds the exact manifest `providerId`; shutdown response contains only the envelope. Snapshot response has `samples`, not `selections` or `providerId`. Requests are monotonically increasing positive signed 32-bit IDs. Unknown fields, duplicate responses and unsolicited output fail the provider. Rust adapters must not collect before hello/snapshot; stdin EOF stops the serving loop.

Every request/response carries protocolVersion, a requestId and a message type. Match responses to pending requests; reject unexpected IDs, unknown types, duplicate responses and oversized/invalid frames. Handle CRLF/LF, split reads, broken pipes and EOF without assuming one read equals one message. Use a real bounded framing reader, not an unbounded `ReadLine` allocation.

## Sample semantics

Illustrative response only; its timestamp and value are fixtures, not a real reading:

```json
{
  "protocolVersion": 1,
  "requestId": 2,
  "type": "snapshot",
  "samples": [
    {
      "metricId": "cpu.temperature",
      "sourceId": "example-selected-cpu-package",
      "value": 52.3,
      "unit": "celsius",
      "observedAt": "2026-10-05T10:00:00Z",
      "status": "ok"
    }
  ]
}
```

Use numeric values plus typed units, never provider-supplied HTML, images, format strings or action commands. Missing/error samples carry `value: null`, a defined status and a bounded diagnostic code. Zero is not a missing sentinel; validate it according to the metric's semantics. Reject non-finite, out-of-contract and unrequested values.

Keep source observation time separate from host receipt time. Providers must not restamp old data as fresh. The host applies source-specific age limits, rejects implausible future times, and uses monotonic elapsed time for local deadlines/cache expiration. If a source has no reliable observation timestamp, define its freshness semantics before integrating it.

Temperature/log freshness, frame-time freshness and market quotes need distinct policies. A closed-market quote is not interchangeable with a live trading sample. Display market timestamp/currency and delayed/closed/stale status; expired hardware samples become `--`. Provider exit/failure invalidates live values immediately. Demo/test providers must be visibly identified and explicitly selected.

## Settings and credentials

Portable v1 profiles now accept optional `widgets` on LCD keys: one binding on keys 0–12, up to three distinct bindings on key 13. Existing v1 profiles still load unchanged; older app versions reject the new fields. Bindings reference provider/metric/source/unit plus optional label/precision only. Unknown/missing providers remain unavailable. Dynamic provider settings/exact sensor/process selection need a later typed, bounded contract; scripts or command strings are not configuration.

API keys stay outside profiles, manifests, logs and source archives. Prefer per-user Windows-protected storage; pass only the selected provider's credentials through a restricted host mechanism, never command-line arguments. Redact errors/URLs before logging. Select a documented HTTPS provider and enforce its quotas/entitlements. Credential storage and delivery need implementation and tests before authenticated adapters ship.

## Failure and compatibility

Provider failure changes its widgets to unavailable; it must not terminate the USB/input session or run actions. After protocol errors, timeouts or crashes, stop that provider and show a concise reason. Retry only after Start, profile reload or a saved binding/source change; no automatic restart loop. Network adapters may schedule their next normal poll with bounded backoff/Retry-After; never spin or flood during an outage.

Use explicit protocol and manifest versions. Reject incompatible major versions before collection, and document additive-field negotiation before allowing it. Preserve stable IDs. Ship shared Rust/C# fixture messages and a host conformance check so other languages can participate without sharing binaries or toolchain versions.

Required fixtures: oversized and deeply nested messages, malformed JSON/UTF-8, partial lines/EOF, future/stale timestamps, sensor ambiguity, invalid floats, wrong identity/request/version, hung collection, stderr flood, child exit and cancellation during each phase. Tests launch only purpose-built fixtures and access no USB device, real credentials or live market endpoint.

Related: [architecture](architecture.md), [engineering](engineering.md), [performance](performance.md).
