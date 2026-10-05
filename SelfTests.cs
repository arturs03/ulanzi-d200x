using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace D200xDirect;

public static class SelfTests
{
    public static void Run()
    {
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
        using var archive = new ZipArchive(new MemoryStream(rebuilt));
        Check(archive.GetEntry("manifest.json") != null && archive.Entries.Count(x => x.FullName.EndsWith(".png")) == 14, "complete diagnostic page");
        var png = TestIcon.Render(3);
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
        Console.WriteLine($"PASS: {checks} protocol, input and image checks. No hardware was accessed.");
    }
}
