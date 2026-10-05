using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Security.Cryptography;

namespace D200xDirect.App;

internal static class IconStore
{
    public static Bitmap Load(string reference, string? profileDirectory)
    {
        IconReferences.Validate(reference);
        if (reference.StartsWith("builtin:", StringComparison.Ordinal)) return DrawBuiltIn(reference);
        if (profileDirectory is null) throw new IOException("Custom icons require the profile folder.");
        var path = Resolve(profileDirectory, reference);
        return Decode(ReadBounded(path));
    }

    static byte[] ReadBounded(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length is < 33 or > IconReferences.MaximumPngBytes) throw new IOException("Choose a PNG image up to 2 MB.");
        var data = new byte[(int)file.Length]; file.ReadExactly(data); return data;
    }

    static Bitmap Decode(byte[] bytes)
    {
        var dimensions = IconReferences.InspectPng(bytes);
        using var memory = new MemoryStream(bytes);
        using var source = Image.FromStream(memory, useEmbeddedColorManagement: false, validateImageData: true);
        if (source.Width != dimensions.Width || source.Height != dimensions.Height) throw new IOException("Invalid PNG dimensions.");
        return new Bitmap(source); // Detached from the stream and file; no persistent file lock.
    }

    public static string Import(string sourcePath, string profileDirectory)
    {
        if (!Path.IsPathFullyQualified(sourcePath) || sourcePath.StartsWith(@"\\")) throw new IOException("Choose a local PNG file.");
        if (!sourcePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) throw new IOException("Choose a PNG image.");
        using var original = Decode(ReadBounded(sourcePath));
        var factor = Math.Min(1d, 512d / Math.Max(original.Width, original.Height));
        using var normalized = new Bitmap(Math.Max(1, (int)(original.Width * factor)), Math.Max(1, (int)(original.Height * factor)));
        using (var graphics = Graphics.FromImage(normalized))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(original, new Rectangle(0, 0, normalized.Width, normalized.Height));
        }
        using var memory = new MemoryStream(); normalized.Save(memory, ImageFormat.Png);
        var bytes = memory.ToArray(); IconReferences.InspectPng(bytes);
        var id = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()[..24];
        var reference = $"icons/imported-{id}.png";
        var path = Resolve(profileDirectory, reference);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            if (file.Length == 0) { file.Write(bytes); file.Flush(); }
            else
            {
                if (file.Length != bytes.Length) throw new IOException("An icon with this name already has different contents.");
                var existing = new byte[bytes.Length]; file.ReadExactly(existing);
                if (!existing.SequenceEqual(bytes)) throw new IOException("An icon with this name already has different contents.");
            }
        }
        return reference;
    }

    static string Resolve(string profileDirectory, string reference)
    {
        IconReferences.Validate(reference);
        var root = Path.GetFullPath(profileDirectory);
        var directory = Path.Combine(root, "icons");
        var path = Path.GetFullPath(Path.Combine(root, reference.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Icon is outside the profile's icons folder.");
        foreach (var candidate in new[] { directory, path })
        {
            if ((File.Exists(candidate) || Directory.Exists(candidate)) && (File.GetAttributes(candidate) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Icon folders and files must not be directory links.");
        }
        return path;
    }

    static Bitmap DrawBuiltIn(string reference)
    {
        var bitmap = new Bitmap(128, 128);
        using var g = Graphics.FromImage(bitmap); g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.FromArgb(179, 208, 255), 6) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var blue = new SolidBrush(pen.Color);
        switch (reference)
        {
            case "builtin:screenshot":
                g.DrawLines(pen, new Point[] { new Point(37, 22), new Point(22, 22), new Point(22, 39) }); g.DrawLines(pen, new Point[] { new Point(91, 22), new Point(106, 22), new Point(106, 39) });
                g.DrawLines(pen, new Point[] { new Point(22, 89), new Point(22, 106), new Point(39, 106) }); g.DrawLines(pen, new Point[] { new Point(89, 106), new Point(106, 106), new Point(106, 89) });
                g.DrawRectangle(pen, 37, 43, 54, 39); g.DrawEllipse(pen, 54, 52, 20, 20); break;
            case "builtin:record":
                g.DrawRectangle(pen, 20, 29, 63, 70); g.DrawPolygon(pen, new Point[] { new Point(84, 49), new Point(108, 35), new Point(108, 94), new Point(84, 80) });
                using (var red = new SolidBrush(Color.FromArgb(255, 114, 132))) g.FillEllipse(red, 43, 53, 18, 18); break;
            case "builtin:microphone":
                using (var shape = Theme.Round(new RectangleF(48, 18, 32, 62), 16)) g.DrawPath(pen, shape);
                g.DrawArc(pen, 33, 42, 62, 53, 0, 180); g.DrawLine(pen, 64, 96, 64, 113); g.DrawLine(pen, 45, 113, 83, 113); break;
            case "builtin:application":
                g.DrawRectangle(pen, 22, 25, 84, 78); g.DrawLine(pen, 22, 44, 106, 44); g.FillEllipse(blue, 31, 32, 5, 5); g.FillEllipse(blue, 43, 32, 5, 5); break;
            case "builtin:gamepad":
                using (var shape = Theme.Round(new RectangleF(15, 35, 98, 60), 18)) g.DrawPath(pen, shape);
                g.DrawLine(pen, 34, 65, 57, 65); g.DrawLine(pen, 46, 53, 46, 77); g.FillEllipse(blue, 79, 52, 9, 9); g.FillEllipse(blue, 92, 68, 9, 9); break;
            case "builtin:website":
                g.DrawEllipse(pen, 20, 20, 88, 88); g.DrawEllipse(pen, 44, 20, 40, 88); g.DrawLine(pen, 23, 64, 105, 64); break;
            case "builtin:market":
                g.DrawLines(pen, new Point[] { new Point(23, 25), new Point(23, 103), new Point(106, 103) }); g.DrawLines(pen, new Point[] { new Point(34, 83), new Point(55, 59), new Point(75, 71), new Point(100, 34) }); break;
            case "builtin:temperature":
                g.DrawLines(pen, new Point[] { new Point(49, 84), new Point(49, 24), new Point(77, 24), new Point(77, 84) }); g.DrawArc(pen, 41, 69, 44, 44, -45, 270); g.DrawLine(pen, 63, 42, 63, 87); break;
            case "builtin:usage":
                g.DrawRectangle(pen, 21, 76, 18, 30); g.DrawRectangle(pen, 55, 49, 18, 57); g.DrawRectangle(pen, 89, 22, 18, 84); break;
            case "builtin:horn":
                g.DrawPolygon(pen, new Point[] { new Point(24, 52), new Point(58, 52), new Point(90, 31), new Point(90, 98), new Point(58, 78), new Point(24, 78) });
                g.DrawLine(pen, 46, 80, 46, 108); g.DrawLine(pen, 106, 46, 118, 40); g.DrawLine(pen, 108, 65, 121, 65); g.DrawLine(pen, 106, 84, 118, 90); break;
            default: bitmap.Dispose(); throw new ArgumentException("Unsupported built-in icon.");
        }
        return bitmap;
    }
}
