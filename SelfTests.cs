using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace D200xDirect;

public static class SelfTests
{
    public static void Run()
    {
        Providers.ProviderChecks.RunPure();
        var checks = 0;
        void Check(bool valid, string name) { if (!valid) throw new Exception($"Self-test failed: {name}"); checks++; }
        var payload = Enumerable.Range(0, 500).Select(i => (byte)(i % 256)).ToArray();
        var packet = Protocol.Packet(0x000a, payload);
        Check(packet.Length == 1024 && packet[0] == 0x7c && packet[1] == 0x7c, "control header");
        Check(packet[2] == 0 && packet[3] == 0x0a, "command byte order");
        Check(BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(4)) == 500, "length byte order");
        Check(packet.AsSpan(8, 500).SequenceEqual(payload), "control payload");
        var report = Protocol.WindowsReport(packet);
        Check(report.Length == 1025 && report[0] == 0 && report.AsSpan(1).SequenceEqual(packet), "Windows report ID and length");
        var input = Protocol.Packet(0x0101, new byte[] { 0, 3, 0, 1 });
        var key = Protocol.ParseInput(Protocol.WindowsReport(input));
        Check(key == new InputEvent(3, "key", "press"), "key press decoding");
        input[11] = 0;
        Check(Protocol.ParseInput(Protocol.WindowsReport(input))?.Action == "release", "key release decoding");
        input[9] = 17; input[11] = 2;
        Check(Protocol.ParseInput(Protocol.WindowsReport(input)) == new InputEvent(17, "dial", "left"), "dial turn decoding");
        input[11] = 3;
        Check(Protocol.ParseInput(Protocol.WindowsReport(input))?.Action == "right", "dial direction");
        input[9] = 14;
        Check(Protocol.ParseInput(Protocol.WindowsReport(input)) == null, "reject phantom key");
        Check(Protocol.ParseInput(new byte[3]) == null, "reject truncated reports");
        var bundle = Protocol.TestBundle();
        Check(Protocol.CleanBoundaries(bundle), "firmware boundary workaround");
        var pieces = Protocol.BundlePackets(bundle).ToArray();
        Check(BinaryPrimitives.ReadUInt32LittleEndian(pieces[0].AsSpan(4)) == bundle.Length, "bundle transfer length");
        var rebuilt = pieces[0].Skip(8).Concat(pieces.Skip(1).SelectMany(x => x)).Take(bundle.Length).ToArray();
        Check(rebuilt.SequenceEqual(bundle), "multi-packet ZIP reconstruction");
        var display = Protocol.DisplayPackets(bundle).ToArray();
        Check(display.Length == pieces.Length + 2, "bounded wide-mode commands around page");
        Check(BinaryPrimitives.ReadUInt16BigEndian(display[0].AsSpan(2)) == 0x0006
            && Encoding.ASCII.GetString(display[0], 8, (int)BinaryPrimitives.ReadUInt32LittleEndian(display[0].AsSpan(4))) == "2|0|0|00:00:00|0",
            "wide screen selects image mode, not gauges");
        Check(display[^1].SequenceEqual(display[0]), "wide mode restored after bundle");
        var timedMode = Protocol.ImageModePacket(new TimeOnly(12, 34, 56));
        Check(Encoding.ASCII.GetString(timedMode, 8, (int)BinaryPrimitives.ReadUInt32LittleEndian(timedMode.AsSpan(4))) == "2|0|0|12:34:56|0", "keep-awake preserves image mode with current time");
        Check(display.Skip(1).Take(pieces.Length).Zip(pieces).All(pair => pair.First.SequenceEqual(pair.Second)), "display mode does not corrupt ZIP packets");
        try { Protocol.DisplayPackets(new byte[3000]).First(); throw new Exception("Malformed display emitted a mode command."); }
        catch (ArgumentException) { checks++; }
        using var archive = new ZipArchive(new MemoryStream(rebuilt));
        Check(archive.GetEntry("manifest.json") != null && archive.Entries.Count(x => x.FullName.EndsWith(".png")) == 14, "complete diagnostic page");
        var png = TestIcon.Render(3);
        Check(IconReferences.InspectPng(png) == (196, 196), "PNG dimensions checked before decoding");
        var invalidPng = png.ToArray(); BinaryPrimitives.WriteInt32BigEndian(invalidPng.AsSpan(16), 4096);
        try { IconReferences.InspectPng(invalidPng); throw new Exception("Oversized image accepted."); }
        catch (ArgumentException) { checks++; }
        try { IconReferences.InspectPng(new byte[IconReferences.MaximumPngBytes + 1]); throw new Exception("Oversized PNG bytes accepted."); }
        catch (ArgumentException) { checks++; }
        try { IconReferences.InspectPng(new byte[12]); throw new Exception("Truncated PNG accepted."); }
        catch (ArgumentException) { checks++; }
        Check(png.AsSpan(0, 8).SequenceEqual(new byte[] { 137,80,78,71,13,10,26,10 }), "PNG signature");
        Check(BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)) == 196 && BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)) == 196, "native icon dimensions");
        var invalid = new byte[3000];
        Check(!Protocol.CleanBoundaries(invalid), "reject malformed continuation boundary");
        try { Protocol.BundlePackets(invalid).ToArray(); throw new Exception("Malformed bundle accepted."); }
        catch (ArgumentException) { checks++; }
        var profile = Profiles.Parse("""
            {"schemaVersion":1,"name":"Example","keys":[{"index":3,"label":"Mute","action":{"type":"hotkey","keys":["Ctrl","Alt","Shift","M"]}}],
            "dials":[{"index":17,"right":{"type":"hotkey","keys":["VolumeUp"]}}]}
            """);
        Check(Profiles.ActionFor(profile, new(3, "key", "press"))?.Type == "hotkey", "profile key action mapping");
        Check(Profiles.ActionFor(profile, new(3, "key", "release")) == null, "release does not trigger actions");
        Check(Profiles.ActionFor(profile, new(17, "dial", "right"))?.Keys.SequenceEqual(new[] { "VolumeUp" }) == true, "profile dial action mapping");
        Check(Profiles.Parse(System.Text.Json.JsonSerializer.Serialize(profile, Profiles.JsonOptions)).Name == "Example", "profile round trip");
        var serializedAction = System.Text.Json.JsonSerializer.Serialize(profile.Keys[0].Action, Profiles.JsonOptions);
        Check(!serializedAction.Contains("\"url\"") && !serializedAction.Contains("\"path\""), "saved shortcuts omit fields outside their JSON schema");
        void Reject(string json, string name)
        {
            try { Profiles.Parse(json); }
            catch (Exception error) when (error is ArgumentException or System.Text.Json.JsonException) { checks++; return; }
            throw new Exception($"Self-test failed: {name}");
        }
        Reject("""{"schemaVersion":2}""", "unknown schema version");
        Reject("""{"keys":[{"index":14}]}""", "phantom profile key");
        Reject("""{"keys":[{"index":0},{"index":0}]}""", "duplicate profile key");
        Reject("""{"keys":[{"index":0,"background":"blue"}]}""", "invalid profile color");
        Reject("""{"keys":[{"index":0,"action":{"type":"shell","command":"anything"}}]}""", "script action unsupported");
        Reject("""{"keys":[{"index":0,"action":{"type":"open-url","url":"file:///C:/test.exe"}}]}""", "file URLs unsupported");
        Reject("""{"keys":[{"index":0,"action":{"type":"hotkey","keys":["Ctrl"]}}]}""", "modifier-only hotkey");
        Reject("""{"keys":[{"index":0,"action":{"type":"hotkey","keys":["Ctrl","A","B"]}}]}""", "ambiguous hotkey");
        Reject("""{"keys":[{"index":0,"action":{"type":"hotkey","keys":["Ctrl","ctrl","M"]}}]}""", "duplicate hotkey modifiers");
        Reject("""{"keys":[{"index":0,"action":{"type":"launch","path":"app.exe"}}]}""", "relative launch paths");
        Reject("""{"keys":[{"index":0,"action":{"type":"launch","path":"C:\\test.cmd"}}]}""", "script launch paths");
        Reject("""{"keys":[{"index":0,"action":{"type":"none","keys":["A"]}}]}""", "unused action fields");
        Reject("""{"keys":null}""", "null profile arrays");
        Reject("""{"sideButtons":[{"index":17}]}""", "dial mapped as side button");
        Reject("""{"keys":[{"index":0,"icon":"../outside.png"}]}""", "icon traversal unsupported");
        Reject("""{"keys":[{"index":0,"icon":"icons/../outside.png"}]}""", "nested icon traversal unsupported");
        Reject("""{"keys":[{"index":0,"icon":"C:/secret.png"}]}""", "absolute icon path unsupported");
        Reject("""{"keys":[{"index":0,"icon":"https://example.com/icon.png"}]}""", "remote icon unsupported");
        Reject("""{"keys":[{"index":0,"icon":"builtin:unknown"}]}""", "unknown builtin icon unsupported");
        var iconProfile = Profiles.Parse("""{"keys":[{"index":0,"icon":"builtin:screenshot"},{"index":13,"icon":"icons/custom-icon.png"}]}""");
        Check(iconProfile.Keys[1].Icon == "icons/custom-icon.png", "portable custom icon reference accepted");
        var iconEdited = ProfileEditing.Update(iconProfile, 0, "press", "Screenshot", "#102038", new DeckAction());
        Check(iconEdited.Keys[0].Icon == "builtin:screenshot", "action edits preserve existing icon");
        iconEdited = ProfileEditing.Update(iconProfile, 0, "press", "Screenshot", "#102038", new DeckAction(), new KeyAppearance(null));
        Check(iconEdited.Keys[0].Icon is null && iconProfile.Keys[0].Icon == "builtin:screenshot", "explicit icon removal preserves original profile");
        foreach (var preset in ActionPresets.All) { ActionCatalog.Default.Validate(preset.CreateAction()); IconReferences.Validate(preset.Icon); }
        Check(ActionPresets.All.First(p => p.Label == "Screenshot").Keys.SequenceEqual(new[] { "Win", "Shift", "S" })
            && ActionPresets.All.First(p => p.Label == "Record").Keys.SequenceEqual(new[] { "Win", "Shift", "R" }), "capture presets preserve Windows region shortcuts");
        Check(IconReferences.BuiltIns.Select(i => i.Id).Distinct().Count() == IconReferences.BuiltIns.Count, "builtin icon IDs unique");
        async Task CheckKeepAwake()
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var writes = 0;
            var readStopped = false;
            try
            {
                await DeviceSession.RunPairAsync(async ct =>
                {
                    try { await Task.Delay(Timeout.Infinite, ct); }
                    finally { readStopped = true; }
                }, ct => DeviceSession.KeepAwakeAsync(_ =>
                {
                    writes++;
                    throw new IOException("simulated write failure");
                }, TimeSpan.FromMilliseconds(10), ct), deadline.Token);
                throw new Exception("Writer failure was swallowed.");
            }
            catch (IOException) { Check(writes == 1 && readStopped, "writer failure cancels reader without retry"); }
            writes = 0;
            try
            {
                await DeviceSession.RunPairAsync(_ => throw new IOException("simulated read failure"),
                    ct => DeviceSession.KeepAwakeAsync(_ => { writes++; return Task.CompletedTask; }, TimeSpan.FromMilliseconds(50), ct), deadline.Token);
                throw new Exception("Reader failure was swallowed.");
            }
            catch (IOException) { Check(writes == 0, "reader failure stops keep-awake before any writes"); }
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            try
            {
                await DeviceSession.KeepAwakeAsync(_ => { writes++; return Task.CompletedTask; }, TimeSpan.FromMilliseconds(10), cancelled.Token);
                throw new Exception("Cancelled heartbeat continued.");
            }
            catch (OperationCanceledException) { Check(writes == 0, "cancelled keep-awake sends no packets"); }
            Check(DeviceSession.KeepAwakeInterval == TimeSpan.FromSeconds(5), "keep-awake limited to 12 small commands per minute");
        }
        CheckKeepAwake().GetAwaiter().GetResult();
        var platform = new RecordingPlatform();
        var context = new ActionContext(new(3, "key", "press"), platform);
        ActionCatalog.Default.ExecuteAsync(new DeckAction { Type = "hotkey", Keys = ["VolumeUp"] }, context, CancellationToken.None).GetAwaiter().GetResult();
        Check(platform.Calls.SequenceEqual(new[] { "hotkey:VolumeUp" }), "module dispatch uses host capability");
        ActionCatalog.Default.ExecuteAsync(new DeckAction { Type = "none" }, context, CancellationToken.None).GetAwaiter().GetResult();
        Check(platform.Calls.Count == 1, "unassigned module has no side effects");
        try { ActionCatalog.Default.ExecuteAsync(new DeckAction { Type = "open-url", Url = "file:///C:/test.exe" }, context, CancellationToken.None).GetAwaiter().GetResult(); throw new Exception("API bypassed validation."); }
        catch (ArgumentException) { Check(platform.Calls.Count == 1, "invalid API action rejected before host access"); }
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            try { ActionCatalog.Default.ExecuteAsync(new DeckAction { Type = "hotkey", Keys = ["VolumeUp"] }, context, cancelled.Token).GetAwaiter().GetResult(); throw new Exception("Cancelled API executed."); }
            catch (OperationCanceledException) { Check(platform.Calls.Count == 1, "cancelled module has no side effects"); }
        }
        var module = BuiltInActions.Create().First();
        try { new ActionCatalog([module, module]); throw new Exception("Duplicate module accepted."); }
        catch (ArgumentException) { checks++; }
        Check(ActionCatalog.Default.Descriptors.Select(d => d.Id).SequenceEqual(new[] { "none", "hotkey", "open-url", "launch" }), "editor and API share supported action catalog");
        var edited = ProfileEditing.Update(profile, 17, "left", "Volume", "", new DeckAction { Type = "hotkey", Keys = ["VolumeDown"] });
        Check(edited.Dials.Single(d => d.Index == 17).Left.Keys.Single() == "VolumeDown"
            && edited.Dials.Single(d => d.Index == 17).Right.Keys.Single() == "VolumeUp"
            && profile.Dials.Single(d => d.Index == 17).Left.Type == "none", "dial edit preserves other gestures and original profile");
        try { ProfileEditing.Update(profile, 3, "press", "Changed", "#102038", new DeckAction { Type = "hotkey", Keys = ["Ctrl"] }); throw new Exception("Invalid edit accepted."); }
        catch (ArgumentException) { Check(profile.Keys.Single(k => k.Index == 3).Label == "Mute", "rejected edit leaves original intact"); }
        var sideEdited = ProfileEditing.Update(profile, 15, "press", "", "", new DeckAction { Type = "open-url", Url = "https://example.com" });
        Check(sideEdited.SideButtons.Single().Action.Type == "open-url" && profile.SideButtons.Count == 0, "missing side mapping created without mutating original");
        try { ProfileEditing.Update(profile, 14, "press", "", "#102038", new DeckAction()); throw new Exception("Phantom editor control accepted."); }
        catch (ArgumentException) { checks++; }
        Console.WriteLine($"PASS: {checks} protocol, input and image checks. No hardware was accessed.");
    }

    sealed class RecordingPlatform : IActionPlatform
    {
        public List<string> Calls { get; } = [];
        public void SendHotkey(IReadOnlyList<string> keys) => Calls.Add("hotkey:" + string.Join('+', keys));
        public void OpenUrl(string url) => Calls.Add("url:" + url);
        public void LaunchApplication(string path) => Calls.Add("launch:" + path);
    }
}
