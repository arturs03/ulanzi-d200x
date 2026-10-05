using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace D200xDirect;

public sealed record InputEvent(int Index, string Kind, string Action);

public static class Protocol
{
    public const int PacketSize = 1024, FirstPayloadSize = 1016;

    public static byte[] Packet(ushort command, ReadOnlySpan<byte> payload, uint? totalLength = null)
    {
        if (payload.Length > FirstPayloadSize) throw new ArgumentException("Control payload exceeds one packet.");
        var result = new byte[PacketSize];
        result[0] = result[1] = 0x7c;
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(2), command);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), totalLength ?? (uint)payload.Length);
        payload.CopyTo(result.AsSpan(8));
        return result;
    }

    public static byte[] WindowsReport(ReadOnlySpan<byte> packet)
    {
        if (packet.Length != PacketSize) throw new ArgumentException("A D200X packet must contain exactly 1024 bytes.");
        var result = new byte[PacketSize + 1]; // Windows requires report ID 0 before the unnumbered report.
        packet.CopyTo(result.AsSpan(1));
        return result;
    }

    public static InputEvent? ParseInput(ReadOnlySpan<byte> report)
    {
        if (report.Length != PacketSize + 1 || report[0] != 0) return null;
        var packet = report[1..];
        if (packet[0] != 0x7c || packet[1] != 0x7c) return null;
        var command = BinaryPrimitives.ReadUInt16BigEndian(packet[2..]);
        if (command is not (0x0101 or 0x0102)) return null;
        var index = packet[9];
        var action = packet[11];
        if (index is >= 17 and <= 19)
            return action <= 3 ? new(index, "dial", new[] { "release", "press", "left", "right" }[action]) : null;
        if (index is <= 13 or 15 or 16)
            return action <= 1 ? new(index, index >= 15 ? "side" : "key", action == 1 ? "press" : "release") : null;
        return null;
    }

    public static byte[] TestBundle()
    {
        // Rebuild with varied padding until every continuation boundary avoids the firmware's header ambiguity.
        for (var attempt = 0; attempt < 256; attempt++)
        {
            using var memory = new MemoryStream();
            using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
            {
                Write(zip, "dummy.txt", Encoding.ASCII.GetBytes(new string('x', attempt)), CompressionLevel.NoCompression);
                var manifest = new Dictionary<string, object>();
                for (var index = 0; index < 14; index++)
                {
                    var name = $"icons/direct-test-{index}.png";
                    Write(zip, name, TestIcon.Render(index), CompressionLevel.NoCompression);
                    manifest[$"{index % 5}_{index / 5}"] = new { State = 0, ViewParam = new[] { new { Text = "", Icon = name } } };
                }
                Write(zip, "manifest.json", JsonSerializer.SerializeToUtf8Bytes(manifest), CompressionLevel.NoCompression);
            }
            var bytes = memory.ToArray();
            if (CleanBoundaries(bytes)) return bytes;
        }
        throw new IOException("Could not form a valid device bundle; nothing will be sent.");
    }

    public static bool CleanBoundaries(ReadOnlySpan<byte> bundle)
    {
        for (var offset = FirstPayloadSize; offset < bundle.Length; offset += PacketSize)
            if (bundle[offset] is 0 or 0x7c) return false;
        return true;
    }

    public static IEnumerable<byte[]> BundlePackets(byte[] bundle)
    {
        if (bundle.Length == 0 || !CleanBoundaries(bundle)) throw new ArgumentException("Invalid bundle boundaries.");
        yield return Packet(0x0001, bundle.AsSpan(0, Math.Min(bundle.Length, FirstPayloadSize)), (uint)bundle.Length);
        for (var offset = FirstPayloadSize; offset < bundle.Length; offset += PacketSize)
        {
            var chunk = new byte[PacketSize];
            bundle.AsSpan(offset, Math.Min(PacketSize, bundle.Length - offset)).CopyTo(chunk);
            yield return chunk;
        }
    }

    public static IEnumerable<byte[]> DisplayPackets(byte[] bundle)
    {
        // Validate the whole transfer before even changing the wide-screen mode.
        if (bundle.Length == 0 || !CleanBoundaries(bundle)) throw new ArgumentException("Invalid bundle boundaries.");
        // Key 13 has a separate firmware renderer. Mode 2 displays our icon instead of
        // the retained CPU/RAM/GPU gauges or clock. Other fields are unused placeholders,
        // not measured sensor values. No telemetry polling or periodic mode refresh occurs.
        var imageMode = Packet(0x0006, Encoding.ASCII.GetBytes("2|0|0|00:00:00|0"));
        yield return imageMode;
        foreach (var packet in BundlePackets(bundle)) yield return packet;
        // Restate once after the page because firmware can retain/reset its wide renderer.
        // This is a bounded display command, never an automatic retry loop.
        yield return imageMode;
    }

    static void Write(ZipArchive zip, string name, byte[] data, CompressionLevel compression)
    {
        var entry = zip.CreateEntry(name, compression);
        entry.LastWriteTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var stream = entry.Open();
        stream.Write(data);
    }
}
