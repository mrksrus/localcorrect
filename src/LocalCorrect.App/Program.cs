using System.Reflection;
using LocalCorrect.App.Win32;
using LocalCorrect.Core;
using LocalCorrect.Engine;

namespace LocalCorrect.App;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        // First action: a log line that depends on nothing but the base library,
        // so a later startup failure is still visible in %LOCALAPPDATA%\LocalCorrect\log.txt.
        var logPath = AppPaths.LogFile();
        StartupLog.WriteFirstLine(logPath);
        try
        {
            ApplicationConfiguration.Initialize();
            if (Has(args, "--install")) return Installer.Install(logPath);
            if (Has(args, "--uninstall")) return Installer.Uninstall(logPath);
            using var mutex = new Mutex(false, @"Local\LocalCorrect.SingleInstance");
            var acquired = false;
            try
            {
                acquired = mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }
            if (!acquired)
            {
                MessageBox.Show("LocalCorrect is already running. Look for its tray icon.", "LocalCorrect", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }
            try
            {
                var settings = SettingsStore.Load(AppPaths.SettingsFile());
                SymSpellCorrector symspell;
                try
                {
                    symspell = SymSpellCorrector.FromEmbeddedLexicons();
                }
                catch (Exception ex)
                {
                    FileLog.Write(logPath, true, "error", "word lists: " + ex.GetType().Name);
                    MessageBox.Show("LocalCorrect could not load its word lists and will exit.", "LocalCorrect", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
                if (settings.Logging)
                    FileLog.Write(logPath, true, "info", "ready en=" + symspell.EnglishWords.ToString() + " de=" + symspell.GermanWords.ToString());
                Application.Run(new TrayController(settings, symspell, logPath));
                return 0;
            }
            finally
            {
                try { mutex.ReleaseMutex(); } catch { /* already released */ }
            }
        }
        catch (Exception ex)
        {
            FileLog.Write(logPath, true, "error", "startup " + ex.GetType().Name);
            try
            {
                MessageBox.Show("LocalCorrect failed to start. See %LOCALAPPDATA%\\LocalCorrect\\log.txt.", "LocalCorrect", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
                // The log line is the diagnostic if even a message box cannot be shown.
            }
            return 1;
        }
    }

    internal static string VersionLabel()
    {
        var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(version)) return "LocalCorrect";
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        if (plus > 0) version = version.Substring(0, plus);
        return "LocalCorrect " + version;
    }

    static bool Has(string[] args, string flag) =>
        args.Any(arg => string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase));
}
