# Executable data-provider protocol

Status: design draft, 2026-10-05. No loader, SDK, manifest schema or wire implementation exists yet. Examples describe a proposed contract; do not add these fields to current profiles. Finalize protocol/schema tests before publishing an API as supported.

## Installation and discovery

Use an app-managed per-user `plugins` directory. Each provider has one folder with `plugin.json`, an executable and optional non-secret resources. The precise installed path and install/update UI remain implementation work.

```text
plugins/
  hardware/
    plugin.json
    hardware.exe
  markets/
    plugin.json
    markets.exe
```

Discovery reads bounded manifest files without launching anything. Reject malformed manifests, incompatible versions and duplicate IDs visibly. Enablement is stored in host settings, separate from portable profiles. A provider requires explicit enablement in the current app session for the initial implementation; copying a folder or loading a profile cannot enable it. Stop/exit ends supervised processes. Rescan/update disabled providers; do not replace a running executable.

Example manifest:

```json
{
  "manifestVersion": 1,
  "id": "org.example.hardware",
  "name": "Hardware readings",
  "version": "0.1.0",
  "protocolVersion": 1,
  "executable": "hardware.exe",
  "platform": "windows-x64",
  "capabilities": ["read-local-monitor-log"],
  "metrics": [
    { "id": "cpu.temperature", "unit": "celsius", "minimumIntervalMs": 1000 }
  ]
}
```

Version numbers above are proposed versions, independent of profile schemaVersion 1. Metric IDs are stable within a provider; a host binding also identifies the selected source/sensor. Units and IDs are machine-readable, while names are presentation metadata. Document metric ranges/semantics and source-selection options in the provider's metadata/settings contract.

Validate manifest size, UTF-8, known fields, ID syntax/length, versions, metric uniqueness, supported units and interval bounds. Use a fixed local executable filename inside that provider folder; reject absolute/UNC/traversal paths and linked/reparse-point escapes. Launch the resolved executable with `UseShellExecute = false`, explicit working directory, redirected streams and fixed host-owned arguments. Never interpolate a shell command or inherit secrets unnecessarily. Capability declarations are informational, not an OS sandbox.

## Transport and lifecycle

Use a persistent child process with newline-delimited UTF-8 JSON through redirected stdin/stdout. This avoids a network listener and language-specific ABI. Stdout contains protocol messages only; bounded stderr contains diagnostics. Host drains both concurrently so full pipes cannot deadlock the child.

Initial proposed limits, to be confirmed by fixtures and measurements:

| Limit | Proposed default |
| --- | --- |
| Manifest or individual wire message | 64 KiB maximum, enforced before full allocation/parsing |
| JSON nesting | 16 levels maximum |
| Selected metrics per request | 64 maximum |
| Outstanding host requests per provider | One; coalesce newer demand |
| Hello and snapshot deadline | 3 seconds each |
| Graceful shutdown deadline | 2 seconds, then terminate the owned child/process tree |
| Stored diagnostics | 64 KiB ring per process; drain excess without retaining it |

These are engineering starting points, not measured performance claims or a compatibility promise. A source needing longer acquisition must negotiate a documented bounded deadline rather than disabling timeouts globally.

1. Host launches an explicitly enabled provider and sends `hello`; the response identifies the provider and supported protocol. Reject identity/version mismatch.
2. Host sends `snapshot` for the union of selected metrics/sources, no faster than the effective sampling policy. Provider returns one bounded response. No unsolicited values in the initial protocol.
3. Host sends `shutdown` on stop/disable/exit, closes stdin and waits within the deadline. EOF also tells the provider to stop. Host disposes streams/process handles and verifies child exit.

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

Portable profiles reference provider/metric/source IDs and display settings only. The current profile schema rejects these fields; implement an explicit compatible schema/validator/editor change before using bindings. Provider configuration needs typed, bounded settings and exact sensor/process selection, not scripts or command strings.

API keys stay outside profiles, manifests, logs and source archives. Prefer per-user Windows-protected storage; pass only the selected provider's credentials through a restricted host mechanism, never command-line arguments. Redact errors/URLs before logging. Select a documented HTTPS provider and enforce its quotas/entitlements. Credential storage and delivery need implementation and tests before authenticated adapters ship.

## Failure and compatibility

Provider failure changes its widgets to unavailable; it must not terminate the USB/input session or run actions. After protocol errors, timeouts or crashes, stop that provider, show a concise reason and require explicit re-enable. No automatic restart loop. Network adapters may schedule their next normal poll with bounded backoff/Retry-After; never spin or flood during an outage.

Use explicit protocol and manifest versions. Reject incompatible major versions before collection, and document additive-field negotiation before allowing it. Preserve stable IDs. Ship shared Rust/C# fixture messages and a host conformance check so other languages can participate without sharing binaries or toolchain versions.

Required fixtures: oversized and deeply nested messages, malformed JSON/UTF-8, partial lines/EOF, future/stale timestamps, sensor ambiguity, invalid floats, wrong identity/request/version, hung collection, stderr flood, child exit and cancellation during each phase. Tests launch only purpose-built fixtures and access no USB device, real credentials or live market endpoint.

Related: [architecture](architecture.md), [engineering](engineering.md), [performance](performance.md).
