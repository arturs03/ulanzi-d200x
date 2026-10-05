# Resource use and measurement

Low resource use is an accepted product requirement. These policies govern future live providers; the current preview does not collect live widgets. No CPU/RAM budget or game-performance guarantee has been verified. Report measurements rather than inferring efficiency from Rust, executable size or a single Task Manager screenshot.

## Scheduling policy

| Work | Initial design policy |
| --- | --- |
| Providers | Start only when explicitly enabled and selected; stop when unused or the controlling session ends |
| Hardware/usage | Share one acquisition snapshot across related tiles; begin around 1-2 seconds, respecting source limits |
| Markets | Share cache and requests per provider; begin around 1-5 minutes only where quotas/entitlements permit |
| FPS | Optional collector for a selected game; display aggregate readings roughly once a second |
| Rendering | Round/format first; render only when displayed text/status changes; bound image-cache size |
| Screen writes | Opt-in, serialized, changed tiles only after transport validation; bounded queue coalesces obsolete frames |
| Logs/errors | Bounded retention and throttled repeated diagnostics; bounded backoff for normal network polling |

Intervals are starting points to test, not compulsory wakeups. Disabled/unselected providers should do no collection. Disconnect stops physical updates and collection tied to that session; an explicit preview-only session may continue selected acquisition. Minimize idle timers and keep the existing optional keep-screen-on behavior distinct.

Frame tracing can run continuously even if FPS is displayed once a second. Measure the collector, not just its output interval. Reading an existing monitor log does not erase the monitor's hardware access or overhead. Avoid duplicate collectors and full-file scans. Use reusable persistent processes, bounded tails and caches; do not launch an executable for each key/sample.

## Measurement procedure

Use release builds on the same machine, power settings and representative workload. Record commit/app/provider/collector versions, enabled metrics, refresh rates, Windows version and relevant hardware, without personal identifiers. Distinguish cold startup from warmed steady state.

Compare the current host baseline, host stopped/idle, preview collection, opted-in live screens and the same workload with providers disabled. Include a normal desktop session and a reproducible game workload when FPS/GPU collection is involved. Run long enough to observe steady state and cache/log behavior; state the duration and repeat comparisons when noise makes the result inconclusive.

Report at least:

- Total CPU time over elapsed time, with the normalization stated. One-core utilization is `100 * CPU seconds / elapsed seconds`; whole-machine normalization additionally divides by logical processor count.
- Private committed bytes and working set per process plus host/provider/collector totals, including peaks and trend.
- Threads, handles, allocations/GC where relevant, process startups, network requests/bytes and disk I/O.
- Image renders, USB write count/bytes, queue depth/coalescing and timeout/failure counts.
- For game comparisons, frame-time distribution and consistent percentile/tail metrics alongside average FPS. Record the measurement tool's own overhead.
- Stop/disable/disconnect cleanup: children exit, queues empty and polling/writes stop. Check repeated enable/disable sessions for resource growth.

External monitors and tracing services must be counted or listed as an explicit unmeasured dependency. Do not subtract their cost because they already run on a developer's machine. Avoid claiming zero idle CPU, zero gaming impact or a fixed memory footprint without measurements.

## Review and release expectations

Provider, dependency, render and scheduler changes should include before/after results or state why measurement is still pending. Explain a regression's cause and tradeoff before accepting it. Define numeric budgets from representative baseline data rather than inventing them now. A working provider is not release-ready if it spins during outages, leaks processes, grows memory/queues without bounds or repeatedly resends a page.

Physical verification must confirm updates, input responsiveness, no observed flicker and stop/error behavior on the D200X. Automated resource checks cannot establish hardware stability. See [architecture](architecture.md), [engineering](engineering.md) and [hardware validation](hardware-validation.md).
