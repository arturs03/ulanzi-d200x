namespace D200xDirect.App;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.SequenceEqual(new[] { "--ui-check" }))
        {
            var fixtureDirectory = Path.Combine(AppContext.BaseDirectory, "ui-check-profile");
            Directory.CreateDirectory(fixtureDirectory);
            var starter = Path.Combine(AppContext.BaseDirectory, "profiles", "starter.json");
            File.Copy(starter, Path.Combine(fixtureDirectory, "profile.json"), true);
            IconChecks.Run(fixtureDirectory);
            var profile = Profiles.Parse(File.ReadAllText(starter));
            var bundle = ProfileImages.Bundle(profile, fixtureDirectory);
            if (!Protocol.CleanBoundaries(bundle)) throw new IOException("Invalid profile display bundle.");
            using (var zip = new System.IO.Compression.ZipArchive(new MemoryStream(bundle)))
            {
                if (zip.GetEntry("manifest.json") is null || zip.Entries.Count(e => e.FullName.EndsWith(".png")) != 14)
                    throw new IOException("Incomplete profile display bundle.");
                foreach (var entry in zip.Entries.Where(e => e.FullName.EndsWith(".png")))
                {
                    using var png = entry.Open();
                    using var icon = Image.FromStream(png);
                    if (icon.Width != 196 || icon.Height != 196) throw new IOException("Invalid encoded icon dimensions.");
                    if (entry.FullName == "icons/key-13.png")
                    {
                        using var physical = new Bitmap(392, 196);
                        using var graphics = Graphics.FromImage(physical);
                        graphics.DrawImage(icon, new Rectangle(0, 0, 392, 196));
                        physical.Save(Path.Combine(AppContext.BaseDirectory, "ui-check-wide-native.png"), System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
            }
            using var form = new MainForm(fixtureDirectory);
            form.Show();
            Application.DoEvents();
            Capture("ui-check.png");
            form.CheckEditor();
            form.CheckProfileImport();
            Application.DoEvents();
            form.PerformLayout();
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "ui-check-layout.txt"), $"DPI {form.DeviceDpi}; size {form.ClientSize}; scaling {form.AutoScaleDimensions}");
            Capture("ui-check-editor.png");
            form.Size = form.MinimumSize;
            Application.DoEvents();
            form.CheckCompactLayout();
            Capture("ui-check-compact.png");
            form.Close();
            return;

            void Capture(string name)
            {
                form.PerformLayout();
                using var image = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
                image.Save(Path.Combine(AppContext.BaseDirectory, name), System.Drawing.Imaging.ImageFormat.Png);
            }
        }
        using var instance = new Mutex(true, "Local\\D200xDirect.App", out var first);
        if (!first) { MessageBox.Show("D200X Direct is already open.", "D200X Direct"); return; }
        Application.Run(new MainForm());
    }
}
