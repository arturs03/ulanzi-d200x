using System.Drawing.Imaging;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace D200xDirect.App;

internal static class ProfileImages
{
    public static byte[] Render(KeyConfig key, string? profileDirectory = null)
    {
        // The firmware stretches key 13's 196x196 PNG across its 392x196 screen.
        // Compose at the physical aspect ratio, then squeeze only the encoded image.
        var width = key.Index == 13 ? 392 : 196;
        using var bitmap = new Bitmap(width, 196);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(ColorTranslator.FromHtml(key.Background));
        using var font = new Font("Segoe UI", 22, FontStyle.Bold, GraphicsUnit.Pixel);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisWord };
        if (key.Icon is null) graphics.DrawString(key.Label, font, Brushes.White, new RectangleF(10, 10, width - 20, 150), format);
        else
        {
            using var icon = IconStore.Load(key.Icon, profileDirectory);
            var box = width == 196 ? new RectangleF(44, 10, 108, 108) : new RectangleF(16, 26, 128, 128);
            var factor = Math.Min(box.Width / icon.Width, box.Height / icon.Height);
            var target = new RectangleF(box.X + (box.Width - icon.Width * factor) / 2, box.Y + (box.Height - icon.Height * factor) / 2, icon.Width * factor, icon.Height * factor);
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; graphics.DrawImage(icon, target);
            using var labelFont = new Font("Segoe UI", 18, FontStyle.Bold, GraphicsUnit.Pixel);
            graphics.DrawString(key.Label, labelFont, Brushes.White, width == 196 ? new RectangleF(10, 120, 176, 42) : new RectangleF(154, 12, width - 166, 142), format);
        }
        using var numberFont = new Font("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Pixel);
        graphics.DrawString(key.Index.ToString("D2"), numberFont, Brushes.LightGray, new RectangleF(10, 164, width - 20, 22), format);
        using var output = new MemoryStream();
        if (width == 196) bitmap.Save(output, ImageFormat.Png);
        else
        {
            using var encoded = new Bitmap(196, 196);
            using var scaled = Graphics.FromImage(encoded);
            scaled.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            scaled.DrawImage(bitmap, new Rectangle(0, 0, 196, 196), new Rectangle(0, 0, width, 196), GraphicsUnit.Pixel);
            encoded.Save(output, ImageFormat.Png);
        }
        return output.ToArray();
    }

    public static byte[] Bundle(DeckProfile profile, string? profileDirectory = null)
    {
        var images = Enumerable.Range(0, 14).Select(i =>
            (Key: profile.Keys.FirstOrDefault(k => k.Index == i) ?? new KeyConfig { Index = i }, Name: $"icons/key-{i}.png"))
            .Select(item => (item.Key, item.Name, Bytes: Render(item.Key, profileDirectory))).ToArray();
        for (var padding = 0; padding < 256; padding++)
        {
            using var memory = new MemoryStream();
            using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
            {
                Write(zip, "dummy.txt", Encoding.ASCII.GetBytes(new string('x', padding)));
                var manifest = new Dictionary<string, object>();
                foreach (var image in images)
                {
                    Write(zip, image.Name, image.Bytes);
                    manifest[$"{image.Key.Index % 5}_{image.Key.Index / 5}"] = new { State = 0, ViewParam = new[] { new { Text = "", Icon = image.Name } } };
                }
                Write(zip, "manifest.json", JsonSerializer.SerializeToUtf8Bytes(manifest));
            }
            var bundle = memory.ToArray();
            if (Protocol.CleanBoundaries(bundle)) return bundle;
        }
        throw new IOException("Could not form valid display transfer boundaries. Nothing was sent.");
    }

    static void Write(ZipArchive zip, string name, byte[] bytes)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.NoCompression);
        entry.LastWriteTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var stream = entry.Open();
        stream.Write(bytes);
    }
}
