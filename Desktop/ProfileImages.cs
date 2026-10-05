using System.Drawing.Imaging;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace D200xDirect.App;

internal static class ProfileImages
{
    public static byte[] Render(KeyConfig key)
    {
        using var bitmap = new Bitmap(196, 196);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(ColorTranslator.FromHtml(key.Background));
        using var font = new Font("Segoe UI", 22, FontStyle.Bold, GraphicsUnit.Pixel);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisWord };
        graphics.DrawString(key.Label, font, Brushes.White, new RectangleF(10, 10, 176, 150), format);
        using var numberFont = new Font("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Pixel);
        graphics.DrawString(key.Index.ToString("D2"), numberFont, Brushes.LightGray, new RectangleF(10, 164, 176, 22), format);
        using var output = new MemoryStream();
        bitmap.Save(output, ImageFormat.Png);
        return output.ToArray();
    }

    public static byte[] Bundle(DeckProfile profile)
    {
        var images = Enumerable.Range(0, 14).Select(i =>
            (Key: profile.Keys.FirstOrDefault(k => k.Index == i) ?? new KeyConfig { Index = i }, Name: $"icons/key-{i}.png"))
            .Select(item => (item.Key, item.Name, Bytes: Render(item.Key))).ToArray();
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
