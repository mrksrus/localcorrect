namespace LocalCorrect.Core;

/// <summary>
/// Per-user locations. Settings live in roaming app data. The log and the
/// installed program live in local app data, which is the right place for a
/// machine-local cache and an unsigned tray exe.
/// </summary>
public static class AppPaths
{
    public const string FolderName = "LocalCorrect";
    public const string ExeName = "LocalCorrect.exe";

    public static string RoamingDirectory(string roamingAppData) =>
        Path.Combine(roamingAppData, FolderName);

    public static string SettingsFile(string roamingAppData) =>
        Path.Combine(RoamingDirectory(roamingAppData), "settings.json");

    public static string LocalDirectory(string localAppData) =>
        Path.Combine(localAppData, FolderName);

    public static string LogFile(string localAppData) =>
        Path.Combine(LocalDirectory(localAppData), "log.txt");

    public static string InstallDirectory(string localAppData) =>
        Path.Combine(LocalDirectory(localAppData), "app");

    public static string InstalledExe(string localAppData) =>
        Path.Combine(InstallDirectory(localAppData), ExeName);

    public static bool IsInstalledLocation(string exePath, string localAppData)
    {
        var installed = Path.GetFullPath(InstalledExe(localAppData));
        var current = Path.GetFullPath(exePath);
        return string.Equals(installed, current, StringComparison.OrdinalIgnoreCase);
    }

    public static string RoamingDirectory() =>
        RoamingDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

    public static string SettingsFile() => SettingsFile(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

    public static string LocalDirectory() =>
        LocalDirectory(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    public static string LogFile() => LogFile(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    public static string InstallDirectory() =>
        InstallDirectory(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    public static string InstalledExe() =>
        InstalledExe(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
}
