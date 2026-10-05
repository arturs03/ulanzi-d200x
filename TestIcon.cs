using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace D200xDirect;

public static class TestIcon
{
    const int Size = 196;
    static readonly Dictionary<char, string[]> Font = new()
    {
        ['T'] = ["11111", "00100", "00100", "00100", "00100", "00100", "00100"],
        ['E'] = ["11111", "10000", "10000", "11110", "10000", "10000", "11111"],
        ['S'] = ["01111", "10000", "10000", "01110", "00001", "00001", "11110"],
        ['0'] = ["01110", "10001", "10011", "10101", "11001", "10001", "01110"],
        ['1'] = ["00100", "01100", "00100", "00100", "00100", "00100", "01110"],
        ['2'] = ["01110", "10001", "00001", "00010", "00100", "01000", "11111"],
        ['3'] = ["11110", "00001", "00001", "01110", "00001", "00001", "11110"],
        ['4'] = ["00010", "00110", "01010", "10010", "11111", "00010", "00010"],
        ['5'] = ["11111", "10000", "10000", "11110", "00001", "00001", "11110"],
        ['6'] = ["01110", "10000", "10000", "11110", "10001", "10001", "01110"],
        ['7'] = ["11111", "00001", "00010", "00100", "01000", "01000", "01000"],
        ['8'] = ["01110", "10001", "10001", "01110", "10001", "10001", "01110"],
        ['9'] = ["01110", "10001", "10001", "01111", "00001", "00001", "01110"]
    };

    public static byte[] Render(int index)
    {
        var pixels = new byte[(Size * 3 + 1) * Size];
        for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var offset = y * (Size * 3 + 1) + 1 + x * 3;
                pixels[offset] = 12; pixels[offset + 1] = 30; pixels[offset + 2] = 55;
            }
        Draw(pixels, "TEST", 4, 34);
        Draw(pixels, index.ToString("D2"), 10, 91);
        using var png = new MemoryStream();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, Size);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), Size);
        header[8] = 8; header[9] = 2; // RGB, no alpha.
        Chunk(png, "IHDR", header);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, true)) zlib.Write(pixels);
        Chunk(png, "IDAT", compressed.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    static void Draw(byte[] pixels, string text, int scale, int top)
    {
        var left = (Size - (text.Length * 6 - 1) * scale) / 2;
        for (var letter = 0; letter < text.Length; letter++)
            for (var y = 0; y < 7; y++)
                for (var x = 0; x < 5; x++)
                    if (Font[text[letter]][y][x] == '1')
                        for (var sy = 0; sy < scale; sy++)
                            for (var sx = 0; sx < scale; sx++)
                            {
                                var offset = (top + y * scale + sy) * (Size * 3 + 1) + 1 + (left + (letter * 6 + x) * scale + sx) * 3;
                                pixels[offset] = pixels[offset + 1] = pixels[offset + 2] = 240;
                            }
    }

    static void Chunk(Stream stream, string name, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        var type = Encoding.ASCII.GetBytes(name);
        stream.Write(type); stream.Write(data);
        uint crc = 0xffffffff;
        foreach (var value in type.Concat(data))
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320);
        }
        BinaryPrimitives.WriteUInt32BigEndian(length, ~crc);
        stream.Write(length);
    }
}
