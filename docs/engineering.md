# Rust and C# engineering standards

These are project-specific review expectations. Prefer small, readable changes with explicit ownership and evidence. Avoid a framework, abstraction or dependency unless it solves a current problem. Preserve behavior and public configuration unless a migration is intentional and documented.

## Boundaries and review

- Keep validation/formatting pure and acquisition/side effects behind small interfaces. Introduce interfaces at testable external boundaries, not for every class.
- Make states and errors explicit: stopped, running, cancelling, unavailable and failed are different. Use stable codes for machine behavior and useful text for users; never parse error text to make control decisions.
- Decide who owns each task, process, handle, cache and queue, and how stop/error disposes it. Cancellation without joining work is not completed cleanup.
- Validate before mutation. Invalid configuration must leave the last valid profile/selection intact; loading/saving must not execute actions or write screens.
- Keep profile validation, public schema, editor and documentation aligned. Old v1 profiles must still load unless an explicit migration is introduced.
- Explain unusual protocol/native details with their invariant and evidence. Avoid comments that merely repeat code or broad refactors bundled with a feature.

## Rust providers

No Rust workspace exists yet. When adding the first provider, define a stable toolchain in `rust-toolchain.toml`, its minimum supported Rust version in `Cargo.toml`, and commit `Cargo.lock` for the executable/workspace. Keep build output ignored; end users install binaries, not Rust.

- Prefer safe Rust and explicit ownership. Begin with `#![forbid(unsafe_code)]` for pure protocol/parsing crates. Where native interop needs unsafe, isolate it in a small adapter with documented `SAFETY` invariants, exact ABI/handle lifetime rules and focused tests. Do not relax safety across the workspace for one adapter. Native dependencies are still part of the trust boundary.
- Use `Result` for expected I/O, parsing, absent sensors, rate limits and disconnects; use `Option` for meaningful absence. Avoid `unwrap`, `expect` and panic in external-input paths. A proven internal invariant can justify `expect` with an explanation; tests may use it.
- Model units, metric IDs, sample status and source selection with types/enums. Validate at construction. Do not stringly type lifecycle state or substitute zero for a failed sample.
- Borrow/reuse buffers where helpful. Avoid cloning large snapshots, accumulating log files in memory or allocating on every frame without evidence. Use bounded queues/caches and stream/tail readers with enforced limits.
- Prefer a simple blocking worker with cancellation-aware waits for a small sequential collector. Adopt an async runtime only when required concurrency justifies it. Never block an async executor, hold a mutex across await, detach unowned threads/tasks or poll in a busy loop.
- Keep stdout exclusively for protocol JSON. Flush complete frames; send sanitized diagnostics to stderr. Observe stdin EOF/shutdown and stop/join collectors. Failed native calls must not keep returning the previous value as live.
- Audit dependencies for purpose, license, maintenance, build scripts/native requirements and feature cost. Disable unused default features where appropriate. Do not hand-roll TLS, JSON parsing or cryptography to reduce dependency count.
- Use release builds for measurements. Apply LTO, stripping, custom allocators or panic strategy only with a documented reason and measured tradeoff. Never assume a small executable means low working set.

Once a workspace exists, run from its root:

```powershell
cargo fmt --all -- --check
cargo clippy --workspace --all-targets --locked -- -D warnings
cargo test --workspace --locked
cargo build --workspace --release --locked
```

Test supported feature combinations separately when they differ from defaults. Do not invent passing Rust checks while there is no toolchain/workspace. Pinning and dependency vulnerability/license checks belong in the Rust rollout; they are not implemented CI today.

References: [Rust error handling](https://doc.rust-lang.org/book/ch09-03-to-panic-or-not-to-panic.html), [ownership/concurrency](https://doc.rust-lang.org/book/ch16-03-shared-state.html), [Cargo lockfiles](https://doc.rust-lang.org/cargo/guide/cargo-toml-vs-cargo-lock.html).

## C# host

Target the existing .NET 8 projects. Keep nullable analysis enabled, address new warnings and follow the surrounding naming/style. A new analyzer or framework version needs a focused change rather than a silent build dependency.

- Keep UI callbacks short. Perform process/network/file acquisition off the UI thread and post only bounded presentation updates. Coalesce updates so a fast provider cannot flood the WinForms message queue.
- Observe WinForms thread affinity with the existing .NET 8-compatible marshaling approach, including closing/disposed checks and session identity. `Control.InvokeAsync` requires .NET 9+; do not copy newer documentation into this .NET 8 app without a deliberate migration.
- Prefer Task-returning methods; reserve `async void` for UI events with handled failures. Avoid `.Wait()`, `.Result`, unobserved fire-and-forget tasks and overlapping timer callbacks. Track and await session work on shutdown without blocking the UI message pump.
- Propagate `CancellationToken` through I/O and scheduling. Distinguish user cancellation from an operation deadline and a device/provider failure. Dispose linked cancellation sources. A timeout must actually stop the operation, not merely stop awaiting it.
- Keep immutable validated snapshots at worker/UI boundaries. Use one clear owner or explicit synchronization for mutable state; avoid holding locks across awaits or making Windows calls under broad locks.
- Dispose streams, child processes, images, fonts, native handles and timers deterministically. Retain `SafeHandle`-based native lifetime management; validate P/Invoke layouts, sizes, calling convention and Windows error handling. Do not modernize interop mechanically without targeted verification.
- Reuse an HTTP client when network adapters are added. Bound response sizes, deadlines and redirects; enforce HTTPS/provider selection and quotas. External messages remain untrusted even on a pipe.
- Launch providers without a shell, with a resolved validated local path, fixed arguments and simultaneously drained stdout/stderr. Avoid secrets in command lines and manage only the owned child/process tree.
- Keep the device guard, Studio-running check and report capability selection intact. A future scheduler must serialize all writes, including keep-screen-on work. Never let a data provider write USB or inject input.
- Keep CPU/GDI rendering and bounded PNG rules. Cache by displayed output, dispose evicted images, and preserve key 13's double-width composition/device encoding and content-based filenames.
- Catch exceptions where recovery/reporting is meaningful; do not blanket-catch and continue a broken device session. Preserve the first meaningful failure while cleaning up. Redact USB paths, tokens and provider response details from public diagnostics.

References: [WinForms thread affinity](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/how-to-make-thread-safe-calls), [native interop and SafeHandle](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices).

## Validation proportional to the change

Use the existing `./build.ps1` for C# code changes; it runs hardware-free core checks and builds the desktop. `./run.ps1 -Mode self-test` repeats the core checks. UI/icon/import changes also require the existing isolated `--ui-check` path and visual inspection. Installer/export changes require payload/archive checks. Documentation-only changes need link/contract/status checks, not physical device access.

Add meaningful tests for external parsing, ownership, cancellation and compatibility. Cover malformed input, stale/ambiguous readings, partial writes, bounded queues, hung/exited children and shutdown. Inject clocks/sources/platform adapters where those external boundaries would otherwise make checks flaky or invasive. Do not write tests that merely repeat implementation details or document wording.

Keep automated checks hardware-free by default: no HID handle, live market request, credential access, hotkey, launch of a real application or screen capture. Process lifecycle fixtures are purpose-built test executables. Physical tests are deliberate, recorded separately and never inferred from a successful build.

Changes affecting collectors, allocation, rendering, scheduling or dependencies need [performance evidence](performance.md). A pull request should state the concrete behavior change, validation, compatibility and remaining physical limitations.

## Open-source hygiene

Keep portable templates free of local application paths, sensor IDs tied to a user's setup, credentials, device identifiers and personal logs/dumps. Do not redistribute proprietary Studio artwork. Include third-party licenses and document optional collector/driver requirements. Updating a dependency or adding a source directory also requires checking release/export inclusion; do not ship caches, fixture secrets or generated binaries as source.

The project uses MIT licensing for its own source. A provider executable is trusted local code, not a sandbox; language choice does not change this. Follow [stability](stability.md) and [architecture](architecture.md) for setup/device boundaries.
