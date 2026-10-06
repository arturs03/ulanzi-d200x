# Resource use and measurement

Low resource use is an accepted product requirement. Preview 8 collects optional CPU/RAM app-preview values through one shared Rust provider; later sources and physical refresh remain pending. No resource budget or game-performance guarantee has been verified. Report measurements rather than inferring efficiency from Rust, executable size or one Task Manager screenshot.

## First provider measurement (2026-10-05)

A separate deliberate release-build CLI integration probe took 12 CPU/RAM snapshots at one-second intervals and sampled the warmed CLI/provider processes for 9.84 seconds. Provider peak private bytes were 1.48 MiB and working set 8.75 MiB; the CLI was 11.62 MiB private / 39.16 MiB working set. Warmed CLI CPU was 1.430% of one core; provider CPU was below the reported process-counter resolution (0.000% at three decimals), not evidence of zero cost. Peak provider handle count was 100, and both processes exited normally.

This short probe excludes the WinForms host, startup peaks, external monitors, device rendering/writes and game workloads. It demonstrates acquisition/cleanup and an initial process footprint, not a before/after performance claim or acceptable final budget. Longer desktop-host, repeated-session and representative-workload comparisons remain pending. Automated checks continue to use synthetic fixtures rather than this live integration probe.

## Scheduling policy

Preview 14 renders snapshots only on explicit Update screens/Test selected screen requests. It captures bounded samples on the UI thread, rechecks observation time before drawing, renders on a worker and validates packets before writes. Snapshot preparation can wait up to four seconds without blocking the UI; no image render/upload timer or periodic full-page fallback is added. The one-key probe caps ZIP payloads at 64 KiB/65 reports. Actual physical latency, packet overhead, allocation/CPU cost and flicker remain to be measured; these bounds are not performance evidence.

Preview 13 fixes an observed host scheduling error: nominal one-second Windows delays can return before the declared minimum, which previously stopped collection. The supervisor now rechecks a monotonic clock after each cancellable delay and sends only when the interval has elapsed. It retains one pending request during the wait, does not poll hardware while waiting and adds no automatic restart loop. Timer/fixture checks establish cadence and ownership, not a new resource budget or gaming-performance claim.

Preview 11's temperature reader, extended in preview 16 for GPU Core load, shares one file snapshot for all selected log sensors. Each poll bounds the prefix and tail to 64 KiB each, then rechecks the bounded header (maximum 192 KiB total). No file scan grows with log size, and selecting CPU/RAM alone performs no log I/O. GPU load matching reads only bounded header metadata once per collector startup when no load selection is saved; it launches no monitor. Synthetic release checks verify parser/IPC cleanup; actual desktop-plus-monitor CPU/memory/disk overhead, game impact and before/after measurements remain pending. Do not treat these byte limits as a measured performance budget.

| Work | Initial design policy |
| --- | --- |
| Providers | Start automatically for saved hardware assignments in the shown app; serialize/coalesce changes, stop when unused/paused or the app exits |
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

Preview 12 makes assignment drive collection automatically. Synthetic desktop checks cover startup, saved/shared assignments, rapid edits, removal of the last binding, global Stop, failure without restart loops and queued/active exit. Real desktop/monitor resource measurements remain pending; default-on collection must be included in idle comparisons whenever profiles contain hardware bindings. Empty assignments do not start a provider.

Preview 9's UI uses one worker per assigned provider and awaits each presentation delivery to bound queued UI updates. Fixture checks cover repeated stop, failure and app exit. The earlier CLI/system-provider measurement describes preview 8 only; preview 9's desktop CPU/memory, additional-provider totals and gaming comparisons have not been measured. No performance guarantee follows from the editor/lifecycle checks.

Provider, dependency, render and scheduler changes should include before/after results or state why measurement is still pending. Explain a regression's cause and tradeoff before accepting it. Define numeric budgets from representative baseline data rather than inventing them now. A working provider is not release-ready if it spins during outages, leaks processes, grows memory/queues without bounds or repeatedly resends a page.

Physical verification must confirm updates, input responsiveness, no observed flicker and stop/error behavior on the D200X. Automated resource checks cannot establish hardware stability. See [architecture](architecture.md), [engineering](engineering.md) and [hardware validation](hardware-validation.md).
