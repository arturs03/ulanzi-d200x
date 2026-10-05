using System.Text.Json;
using D200xDirect;

try
{
    if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("This helper uses the Windows HID API.");
    var command = args.FirstOrDefault() ?? "inspect";
    if (command is "help" or "--help" or "-h")
    {
        Console.WriteLine("""
            D200X Direct — experimental Windows USB diagnostic controller
            Commands:
              inspect              List connected D200X HID interfaces (default).
              self-test            Run automated checks without hardware access.
              listen [seconds]     Log buttons/dials; fully exit Studio first.
              test-page [seconds]  Send temporary TEST 00–13 images, then listen.
              preview <PNG path>   Save a test icon without accessing hardware.
              --version            Print app version.
            seconds: 1–3600, default 30. This app has no action mappings yet.
            The test page changes the device display. Physical operation is unverified.
            """);
        return 0;
    }
    if (command == "--version")
    {
        Console.WriteLine(typeof(Protocol).Assembly.GetCustomAttributes(false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion);
        return 0;
    }
    if (command is not ("inspect" or "self-test" or "preview" or "listen" or "test-page"))
        throw new ArgumentException("Unknown command. Use --help to list commands.");
    if (command is "inspect" or "self-test" && args.Length > 1)
        throw new ArgumentException($"Usage: {command}");
    if (command is "listen" or "test-page" && args.Length > 2)
        throw new ArgumentException($"Usage: {command} [seconds]");
    if (command == "self-test") { SelfTests.Run(); return 0; }
    if (command == "preview")
    {
        if (args.Length != 2) throw new ArgumentException("Usage: preview <PNG path>");
        File.WriteAllBytes(args[1], TestIcon.Render(0));
        Console.WriteLine($"Saved diagnostic icon: {Path.GetFullPath(args[1])}");
        return 0;
    }
    var devices = HidDevice.Enumerate();
    if (command == "inspect")
    {
        Console.WriteLine(JsonSerializer.Serialize(devices, new JsonSerializerOptions { WriteIndented = true }));
        return devices.Count > 0 ? 0 : 2;
    }
    using var owner = new Mutex(true, "Local\\D200xDirectController", out var firstController);
    if (!firstController) throw new IOException("Another direct controller is already using the device.");
    if (System.Diagnostics.Process.GetProcessesByName("UlanziDeck").Length > 0)
        throw new IOException("Fully exit Ulanzi Studio before direct input testing.");
    var seconds = args.Length > 1 ? int.Parse(args[1]) : 30;
    if (seconds is < 1 or > 3600) throw new ArgumentOutOfRangeException(nameof(seconds), "Use 1–3600 seconds.");
    var device = devices.SingleOrDefault(x => x.InputLength == 1025 && x.OutputLength == 1025 && x.InputReportIds.SequenceEqual(new[] { 0 }) && x.OutputReportIds.SequenceEqual(new[] { 0 }))
        ?? throw new IOException("Expected one D200X interface with unnumbered 1024-byte reports; inspect capabilities first.");
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
    using var stream = HidDevice.Open(device, command == "test-page");
    if (command == "test-page")
    {
        var bundle = Protocol.TestBundle();
        foreach (var packet in Protocol.BundlePackets(bundle))
            await stream.WriteAsync(Protocol.WindowsReport(packet), timeout.Token);
        await stream.FlushAsync(timeout.Token);
        Console.WriteLine($"Sent a temporary TEST 00–13 page ({bundle.Length} bytes). Studio profiles were not edited. Reopen Studio to restore its page.");
    }
    Console.WriteLine($"Listening for {seconds}s. Press keys and turn/press dials. No hotkeys or application actions will be sent.");
    var buffer = new byte[device.InputLength];
    var events = 0;
    while (!timeout.IsCancellationRequested)
    {
        int length;
        try { length = await stream.ReadAsync(buffer, timeout.Token); }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested) { break; }
        if (length == 0) throw new IOException("Device disconnected.");
        var input = Protocol.ParseInput(buffer.AsSpan(0, length));
        if (input != null)
        {
            events++;
            Console.WriteLine(JsonSerializer.Serialize(new { time = DateTimeOffset.Now, input.Index, input.Kind, input.Action }));
        }
        else Console.WriteLine($"Unrecognized report ({length} bytes): {Convert.ToHexString(buffer.AsSpan(0, Math.Min(length, 24)))}");
    }
    Console.WriteLine($"Finished. Decoded {events} input events.");
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
