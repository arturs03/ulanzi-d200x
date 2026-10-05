using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using Microsoft.Win32;

namespace D200xDirect.Setup;

internal static class Program
{
    const string Product = "D200XDirect";
    const string MarkerName = "installation.json";
    const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\D200XDirect";
    static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Product);
    static string AppDirectory => Path.Combine(Root, "App");
    static string Shortcut => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "D200X Direct.lnk");

    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.SequenceEqual(new[] { "--check-payload" }))
        {
            try { ValidatePayload(); Environment.ExitCode = 0; }
            catch { Environment.ExitCode = 1; }
            return;
        }
        using var form = new Form { Text = "D200X Direct Setup", ClientSize = new Size(540, 310), StartPosition = FormStartPosition.CenterScreen, Font = new Font("Segoe UI", 11), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };
        var title = new Label { Text = "D200X Direct", Font = new Font("Segoe UI", 23, FontStyle.Bold), AutoSize = true, Location = new Point(24, 20) };
        var info = new Label
        {
            Text = "Experimental Windows app for the Ulanzi D200X.\n\nInstalls for your Windows account, with a Start menu shortcut.\nNo administrator access, driver, service or automatic startup.\nProfiles can be edited with ChatGPT or any other LLM.\n\nPhysical device controls and display still need verification.",
            Location = new Point(26, 78), Size = new Size(485, 145)
        };
        var install = new Button { Text = "Install / Update", Location = new Point(26, 241), Size = new Size(145, 38) };
        var remove = new Button { Text = "Uninstall", Location = new Point(186, 241), Size = new Size(115, 38) };
        var close = new Button { Text = "Close", Location = new Point(393, 241), Size = new Size(115, 38) };
        close.Click += (_, _) => form.Close();
        install.Click += (_, _) =>
        {
            install.Enabled = remove.Enabled = false;
            try { Install(); MessageBox.Show(form, "Installed. Open D200X Direct from the Start menu.\nIt starts stopped and does not take control of the device automatically.", "D200X Direct"); }
            catch (Exception error) { MessageBox.Show(form, error.Message, "Installation stopped", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { install.Enabled = remove.Enabled = true; }
        };
        remove.Click += (_, _) =>
        {
            if (MessageBox.Show(form, "Remove D200X Direct? Your saved profiles will be kept.", "D200X Direct", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            try { Uninstall(); form.Close(); }
            catch (Exception error) { MessageBox.Show(form, error.Message, "Uninstall stopped", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };
        form.Controls.AddRange([title, info, install, remove, close]);
        if (args.SequenceEqual(new[] { "--uninstall" })) form.Shown += (_, _) => remove.PerformClick();
        Application.Run(form);
    }

    static ZipArchive OpenPayload()
    {
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("D200XDirect.App.zip")
            ?? throw new IOException("This installer has no app payload. Build it with package.ps1.");
        return new ZipArchive(stream, ZipArchiveMode.Read);
    }

    static void ValidatePayload()
    {
        using var payload = OpenPayload();
        foreach (var entry in payload.Entries) EntryTarget(entry.FullName);
        foreach (var required in new[] { "D200xDirect.exe", "D200xDirect.dll", "profiles/starter.json", "profiles/profile.schema.json", "docs/customization.md", "LICENSE", "DOTNET-LICENSE.txt", "DOTNET-THIRD-PARTY-NOTICES.txt" })
            if (!payload.Entries.Any(e => e.FullName == required)) throw new IOException($"Installer is missing {required}.");
    }

    static string EntryTarget(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Contains(':') || name.Contains('\\') || name.StartsWith('/')
            || name.Split('/').Any(p => p is ".." or ".")) throw new IOException("Invalid installer archive path.");
        var destination = Path.GetFullPath(Path.Combine(AppDirectory, name));
        if (!destination.StartsWith(Path.GetFullPath(AppDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Installer archive path escaped the application folder.");
        return destination;
    }

    static void VerifyRoot(bool mustExist)
    {
        var expected = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Product));
        if (!Path.GetFullPath(Root).Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new IOException("Unexpected installation path.");
        if (!Directory.Exists(Root)) { if (mustExist) throw new IOException("D200X Direct is not installed."); return; }
        if ((File.GetAttributes(Root) & FileAttributes.ReparsePoint) != 0) throw new IOException("Installation folder must not be a junction or link.");
        foreach (var item in Directory.EnumerateFileSystemEntries(Root, "*", SearchOption.AllDirectories))
            if ((File.GetAttributes(item) & FileAttributes.ReparsePoint) != 0) throw new IOException("Installation contains a link; automatic changes stopped.");
        var marker = Path.Combine(Root, MarkerName);
        if (!File.Exists(marker)) throw new IOException("Existing folder is not a recorded D200X Direct installation; it will not be overwritten.");
        using var record = JsonDocument.Parse(File.ReadAllText(marker));
        if (record.RootElement.GetProperty("product").GetString() != Product) throw new IOException("Installation marker does not match.");
    }

    static void EnsureAppClosed()
    {
        var target = Path.GetFullPath(Path.Combine(AppDirectory, "D200xDirect.exe"));
        foreach (var process in Process.GetProcessesByName("D200xDirect"))
        {
            using (process)
            {
                string? path;
                try { path = process.MainModule?.FileName; }
                catch { throw new IOException("Close D200X Direct before installing or removing it."); }
                if (path is not null && Path.GetFullPath(path).Equals(target, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Close D200X Direct through its tray menu, then try again.");
            }
        }
    }

    static void Install()
    {
        ValidatePayload(); VerifyRoot(false); EnsureAppClosed();
        var sourceSetup = Environment.ProcessPath ?? throw new IOException("Cannot find this installer executable.");
        var installedSetup = Path.Combine(Root, "Setup.exe");
        // Verify shortcut ownership before changing an existing shortcut.
        if (File.Exists(Shortcut) && !ShortcutTargetsApp()) throw new IOException("A different Start menu shortcut already uses this name.");
        Directory.CreateDirectory(AppDirectory);
        File.WriteAllText(Path.Combine(Root, MarkerName), JsonSerializer.Serialize(new { product = Product, version = "0.1.0-preview.3" }));
        using (var payload = OpenPayload())
            foreach (var entry in payload.Entries)
            {
                var destination = EntryTarget(entry.FullName);
                if (entry.FullName.EndsWith('/')) { Directory.CreateDirectory(destination); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination, true);
            }
        if (!Path.GetFullPath(sourceSetup).Equals(Path.GetFullPath(installedSetup), StringComparison.OrdinalIgnoreCase))
            File.Copy(sourceSetup, installedSetup, true);
        Directory.CreateDirectory(Path.GetDirectoryName(Shortcut)!);
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new IOException("Windows shortcut creation is unavailable.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic link = shell.CreateShortcut(Shortcut);
        link.TargetPath = Path.Combine(AppDirectory, "D200xDirect.exe");
        link.WorkingDirectory = AppDirectory;
        link.Description = "D200X Direct — edit your profile with any LLM";
        link.Save();
        using var key = Registry.CurrentUser.CreateSubKey(UninstallKey);
        key.SetValue("DisplayName", "D200X Direct (Preview)");
        key.SetValue("DisplayVersion", "0.1.0-preview.3");
        key.SetValue("Publisher", "D200X Direct contributors");
        key.SetValue("InstallLocation", Root);
        key.SetValue("UninstallString", $"\"{installedSetup}\" --uninstall");
        key.SetValue("NoModify", 1); key.SetValue("NoRepair", 1);
    }

    static bool ShortcutTargetsApp()
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")!;
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic link = shell.CreateShortcut(Shortcut);
        return string.Equals((string)link.TargetPath, Path.Combine(AppDirectory, "D200xDirect.exe"), StringComparison.OrdinalIgnoreCase);
    }

    static void Uninstall()
    {
        VerifyRoot(true); EnsureAppClosed();
        // One native PowerShell process performs the deferred deletion, including this running installer.
        // It derives and validates the fixed per-user path itself; no arbitrary target is accepted.
        const string cleanup = """
            $ErrorActionPreference = 'Stop'
            $productPath = [IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'D200XDirect'))
            $expectedPath = [IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'D200XDirect'))
            if ($productPath -ne $expectedPath) { throw 'Unexpected uninstall target.' }
            Wait-Process -Id ([int]$env:D200X_UNINSTALL_PARENT) -ErrorAction SilentlyContinue
            if (-not (Test-Path -LiteralPath $productPath -PathType Container)) { exit 0 }
            $rootItem = Get-Item -LiteralPath $productPath -Force
            if ($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Uninstall target is a link.' }
            $record = Get-Content -LiteralPath (Join-Path $productPath 'installation.json') -Raw | ConvertFrom-Json
            if ($record.product -ne 'D200XDirect') { throw 'Installation marker does not match.' }
            $linkedItems = @(Get-ChildItem -LiteralPath $productPath -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint })
            if ($linkedItems.Count -gt 0) { throw 'Uninstall contains links.' }
            Remove-Item -LiteralPath $productPath -Recurse -Force
            """;
        var helper = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"))
        { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        helper.ArgumentList.Add("-NoProfile"); helper.ArgumentList.Add("-Command"); helper.ArgumentList.Add(cleanup);
        helper.Environment["D200X_UNINSTALL_PARENT"] = Environment.ProcessId.ToString();
        if (File.Exists(Shortcut) && ShortcutTargetsApp()) File.Delete(Shortcut);
        Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
        _ = Process.Start(helper) ?? throw new IOException("Could not start uninstall cleanup.");
    }
}
