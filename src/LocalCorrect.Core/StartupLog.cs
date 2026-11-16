using System.Globalization;

namespace LocalCorrect.Core;

/// <summary>
/// The first line of the log. Uses only the base class library so a failure
/// while settings, word lists, or the tray are loading still leaves a line
/// in %LOCALAPPDATA%\LocalCorrect\log.txt.
/// </summary>
public static class StartupLog
{
    public static void WriteFirstLine(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var line = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture)
                + "Z start pid=" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture)
                + Environment.NewLine;
            File.AppendAllText(path, line);
        }
        catch
        {
            // Starting the tray matters more than the log. Later failures still try to write.
        }
    }
}
