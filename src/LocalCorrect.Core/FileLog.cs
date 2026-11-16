using System.Globalization;

namespace LocalCorrect.Core;

/// <summary>
/// Diagnostic log. Callers must not pass the text the user selected.
/// Info lines are written only when logging is on. Error lines are always written,
/// after the startup line, so a crash is still visible when logging is off.
/// </summary>
public static class FileLog
{
    public static void Write(string path, bool loggingEnabled, string category, string message)
    {
        if (!string.Equals(category, "error", StringComparison.Ordinal) && !loggingEnabled) return;
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var safe = message.Replace('\r', ' ').Replace('\n', ' ');
            if (safe.Length > 500) safe = safe.Substring(0, 500);
            var line = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture)
                + "Z " + category + " " + safe + Environment.NewLine;
            File.AppendAllText(path, line);
        }
        catch
        {
            // Logging must not take down the tray.
        }
    }
}
