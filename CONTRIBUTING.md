# Contributing

Use Windows and the .NET 8 SDK, then run `./build.ps1`. There are no third-party package references. Automated checks run as part of the build and can be repeated with `./run.ps1 -Mode self-test`.

Keep direct HID operations explicit. The default command must only inspect; automated checks must never open a device. Preserve the Studio-running check, single-controller guard and exact report-capability selection. Do not silently add firmware commands, drivers, background startup, network access or keyboard injection.

For a hardware report, include the app version, Windows version, device model and firmware version if known, command used and observed result. Inspect output includes a USB device path; redact device-specific identifiers before posting it publicly. Do not include personal crash dumps or unrelated computer logs.

Distinguish successful compilation and protocol checks from verified physical controls, actual display changes and long-term reliability. Zero decoded events does not establish an input test passed. Identify any untested hardware behavior in your change description.
