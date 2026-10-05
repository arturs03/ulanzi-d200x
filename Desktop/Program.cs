namespace D200xDirect.App;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        using var instance = new Mutex(true, "Local\\D200xDirect.App", out var first);
        if (!first) { MessageBox.Show("D200X Direct is already open.", "D200X Direct"); return; }
        ApplicationConfiguration.Initialize();
        if (args.SequenceEqual(new[] { "--ui-check" }))
        {
            var fixtureDirectory = Path.Combine(AppContext.BaseDirectory, "ui-check-profile");
            Directory.CreateDirectory(fixtureDirectory);
            var starter = Path.Combine(AppContext.BaseDirectory, "profiles", "starter.json");
            File.Copy(starter, Path.Combine(fixtureDirectory, "profile.json"), true);
            var profile = Profiles.Parse(File.ReadAllText(starter));
            var bundle = ProfileImages.Bundle(profile);
            if (!Protocol.CleanBoundaries(bundle)) throw new IOException("Invalid profile display bundle.");
            using (var zip = new System.IO.Compression.ZipArchive(new MemoryStream(bundle)))
                if (zip.GetEntry("manifest.json") is null || zip.Entries.Count(e => e.FullName.EndsWith(".png")) != 14)
                    throw new IOException("Incomplete profile display bundle.");
            using var form = new MainForm(fixtureDirectory);
            form.Show();
            Application.DoEvents();
            form.PerformLayout();
            using var image = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
            image.Save(Path.Combine(AppContext.BaseDirectory, "ui-check.png"), System.Drawing.Imaging.ImageFormat.Png);
            form.Close();
            return;
        }
        Application.Run(new MainForm());
    }
}
