using System.Diagnostics;
using System.Reflection;
using LocalCorrect.Core;

namespace LocalCorrect.App;

internal static class Installer
{
    public static int Install(string logPath)
    {
        try
        {
            var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot find LocalCorrect.exe.");
            var sourceDir = Path.GetDirectoryName(exe) ?? ".";
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var destDir = AppPaths.InstallDirectory(local);
            var destExe = AppPaths.InstalledExe(local);
            var already = AppPaths.IsInstalledLocation(exe, local);
            if (!already)
            {
                Directory.CreateDirectory(destDir);
                CopyTree(sourceDir, destDir);
            }
            var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            Directory.CreateDirectory(programs);
            Shortcut.Create(Path.Combine(programs, "LocalCorrect.lnk"), destExe);
            if (!already)
            {
                Process.Start(new ProcessStartInfo(destExe)
                {
                    WorkingDirectory = destDir,
                    UseShellExecute = true
                });
            }
            MessageBox.Show(
                "LocalCorrect is installed for your Windows user.\n\n" + destDir + "\n\nNo administrator account is needed. Select text in Outlook, a browser, or almost any other window and press Ctrl+Alt+L. Right-click the tray icon to change that.",
                "LocalCorrect",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return 0;
        }
        catch (Exception ex)
        {
            FileLog.Write(logPath, true, "error", "install " + ex.GetType().Name);
            MessageBox.Show(
                "LocalCorrect could not finish installing. If it is already running, right-click the tray icon, choose Exit, and run install.cmd again.\n\n" + ex.Message,
                "LocalCorrect",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
    }

    public static int Uninstall(string logPath)
    {
        try
        {
            try { StartupRegistration.Set(false, ""); } catch { /* still remove the files */ }
            var shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "LocalCorrect.lnk");
            if (File.Exists(shortcut)) File.Delete(shortcut);
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var destDir = AppPaths.InstallDirectory(local);
            var runningHere = AppPaths.IsInstalledLocation(Environment.ProcessPath ?? "", local);
            if (Directory.Exists(destDir) && !runningHere) Directory.Delete(destDir, recursive: true);
            MessageBox.Show(
                runningHere
                    ? "LocalCorrect will finish removing its files after this window closes. Settings in %AppData%\\LocalCorrect were kept."
                    : "LocalCorrect was removed for this user. Settings in %AppData%\\LocalCorrect were kept.",
                "LocalCorrect",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            if (runningHere && Directory.Exists(destDir)) ScheduleDelete(destDir);
            return 0;
        }
        catch (Exception ex)
        {
            FileLog.Write(logPath, true, "error", "uninstall " + ex.GetType().Name);
            MessageBox.Show("LocalCorrect could not be removed completely.\n\n" + ex.Message, "LocalCorrect", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    static void CopyTree(string source, string dest)
    {
        foreach (var file in Directory.GetFiles(source))
        {
            var name = Path.GetFileName(file);
            if (name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)) continue;
            var target = Path.Combine(dest, name);
            if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase)) continue;
            File.Copy(file, target, overwrite: true);
        }
        var licenses = Path.Combine(source, "licenses");
        if (!Directory.Exists(licenses)) return;
        var destLicenses = Path.Combine(dest, "licenses");
        Directory.CreateDirectory(destLicenses);
        foreach (var file in Directory.GetFiles(licenses))
            File.Copy(file, Path.Combine(destLicenses, Path.GetFileName(file)), overwrite: true);
    }

    static void ScheduleDelete(string directory)
    {
        var script = Path.Combine(Path.GetTempPath(), "localcorrect-uninstall-" + Environment.ProcessId.ToString() + ".cmd");
        File.WriteAllText(script, "@echo off\r\nping 127.0.0.1 -n 3 >nul\r\nrmdir /s /q \"" + directory + "\"\r\ndel \"%~f0\"\r\n");
        Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + script + "\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }
}

internal static class Shortcut
{
    public static void Create(string shortcutPath, string targetExe)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Windows Script Host is not available.");
        var shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("Windows Script Host is not available.");
        try
        {
            var shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath })
                ?? throw new InvalidOperationException("Could not create the Start menu shortcut.");
            var shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetExe });
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(targetExe) ?? "" });
            shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "LocalCorrect" });
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
        finally
        {
            if (shell is IDisposable disposable) disposable.Dispose();
        }
    }
}

internal static class StartupRegistration
{
    const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "LocalCorrect";

    public static void Set(bool enabled, string exePath)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(KeyPath, writable: true)
            ?? throw new InvalidOperationException("The per-user startup key is not available.");
        if (enabled)
        {
            if (string.IsNullOrWhiteSpace(exePath)) throw new ArgumentException("Missing program path.", nameof(exePath));
            key.SetValue(ValueName, "\"" + exePath + "\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
