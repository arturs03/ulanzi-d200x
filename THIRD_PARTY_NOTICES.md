# Third-party notices and protocol references

The temperature adapter independently parses the external Open Hardware Monitor CSV format described by its [Logger source](https://github.com/openhardwaremonitor/openhardwaremonitor/blob/master/Utilities/Logger.cs). No Open Hardware Monitor, LibreHardwareMonitor or sibling sensor-bridge code/binaries are redistributed. The external monitor's own licensing/setup and resource use remain separate.

The controller source is an independent C# implementation. It has no third-party NuGet package references. Project C# and new Rust provider source use MIT; see LICENSE. Rust dependencies retain their own licenses. `build-providers.ps1` includes the exact locked dependency inventory and license files in each generated package; the bundled system provider carries these under `plugins/system`. No LHM library, OHM program, Studio SDK or frame collector is bundled by this implementation.

Protocol research references:

- [edubox/opendeck-ulanzi-d200x](https://github.com/edubox/opendeck-ulanzi-d200x): D200X HID commands, input indices and display bundle format.
- [racerxdl/ulanzi-d200-linux](https://github.com/racerxdl/ulanzi-d200-linux): D200 protocol observations and transfer boundaries.
- [Microsoft Windows HID documentation](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/interpreting-hid-reports): Windows report ID and capability handling.

No downloaded controller source is executed or bundled. Ulanzi and D200X are names belonging to their respective owners. This project is independent and is not affiliated with or endorsed by Ulanzi.

Windows packages include Microsoft's .NET and Windows Desktop runtimes. The app payload includes DOTNET-LICENSE.txt, DOTNET-THIRD-PARTY-NOTICES.txt and WINDOWSDESKTOP-LICENSE.txt, taken from the exact runtime build packs. The Windows Desktop pack supplies a LICENSE file and no separate third-party notice file. These licenses apply to the bundled runtimes independently of the controller source license. The installer bundles those same runtimes and notices alongside its embedded app payload.
