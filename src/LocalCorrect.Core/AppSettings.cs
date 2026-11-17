using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalCorrect.Core;

public sealed class AppSettings
{
    public HotkeySpec Hotkey { get; set; } = new();
    public string Language { get; set; } = "auto";
    public string Engine { get; set; } = "languagetool";
    public string LocalModelEndpoint { get; set; } = "http://127.0.0.1:11434";
    public string LocalModelName { get; set; } = "llama3.2";
    public bool PreviewBeforeReplace { get; set; } = true;
    public bool CorrectParagraphWhenNothingSelected { get; set; } = true;
    public List<string> ExcludedProcesses { get; set; } = new();
    public bool StartWithWindows { get; set; }
    public bool Logging { get; set; }

    public AppSettings Clone() => new()
    {
        Hotkey = Hotkey.Clone(),
        Language = Language,
        Engine = Engine,
        LocalModelEndpoint = LocalModelEndpoint,
        LocalModelName = LocalModelName,
        PreviewBeforeReplace = PreviewBeforeReplace,
        CorrectParagraphWhenNothingSelected = CorrectParagraphWhenNothingSelected,
        ExcludedProcesses = new List<string>(ExcludedProcesses),
        StartWithWindows = StartWithWindows,
        Logging = Logging
    };

    public static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return "auto";
        var value = language.Trim().ToLowerInvariant();
        return value is "auto" or "en" or "de" ? value : "auto";
    }

    public static string NormalizeEngine(string? engine)
    {
        // 0.1.0 wrote "symspell" into every settings file; reading it as LanguageTool makes the new default reach existing users.
        var value = (engine ?? "").Trim().ToLowerInvariant();
        return value is "wordlists" or "ollama" ? value : "languagetool";
    }

    public static List<string> NormalizeExclusions(IEnumerable<string>? names)
    {
        var list = new List<string>();
        if (names == null) return list;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            var core = ProcessFilter.CoreName(name);
            if (core.Length == 0 || core.Length > 80) continue;
            if (seen.Add(core)) list.Add(core);
        }
        return list;
    }
}

public static class SettingsStore
{
    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static AppSettings Load(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return new AppSettings();
            var dto = JsonSerializer.Deserialize<SettingsDto>(File.ReadAllText(path), Json);
            return dto == null ? new AppSettings() : dto.ToSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(string path, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(SettingsDto.From(settings), Json);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, path, overwrite: true);
    }

    sealed class SettingsDto
    {
        public HotkeyDto? Hotkey { get; set; }
        public string? Language { get; set; }
        public string? Engine { get; set; }
        public string? LocalModelEndpoint { get; set; }
        public string? LocalModelName { get; set; }
        public bool? PreviewBeforeReplace { get; set; }
        public bool? CorrectParagraphWhenNothingSelected { get; set; }
        public List<string>? ExcludedProcesses { get; set; }
        public bool? StartWithWindows { get; set; }
        public bool? Logging { get; set; }

        public AppSettings ToSettings()
        {
            var settings = new AppSettings();
            if (Hotkey != null)
            {
                var spec = new HotkeySpec
                {
                    Ctrl = Hotkey.Ctrl,
                    Alt = Hotkey.Alt,
                    Shift = Hotkey.Shift,
                    Win = Hotkey.Win,
                    Key = VirtualKeys.Normalize(Hotkey.Key)
                };
                if (spec.IsValid) settings.Hotkey = spec;
            }
            settings.Language = AppSettings.NormalizeLanguage(Language);
            settings.Engine = AppSettings.NormalizeEngine(Engine);
            if (!string.IsNullOrWhiteSpace(LocalModelEndpoint)) settings.LocalModelEndpoint = LocalModelEndpoint.Trim();
            if (!string.IsNullOrWhiteSpace(LocalModelName)) settings.LocalModelName = LocalModelName.Trim();
            if (PreviewBeforeReplace.HasValue) settings.PreviewBeforeReplace = PreviewBeforeReplace.Value;
            if (CorrectParagraphWhenNothingSelected.HasValue) settings.CorrectParagraphWhenNothingSelected = CorrectParagraphWhenNothingSelected.Value;
            settings.ExcludedProcesses = AppSettings.NormalizeExclusions(ExcludedProcesses);
            if (StartWithWindows.HasValue) settings.StartWithWindows = StartWithWindows.Value;
            if (Logging.HasValue) settings.Logging = Logging.Value;
            return settings;
        }

        public static SettingsDto From(AppSettings settings) => new()
        {
            Hotkey = new HotkeyDto
            {
                Ctrl = settings.Hotkey.Ctrl,
                Alt = settings.Hotkey.Alt,
                Shift = settings.Hotkey.Shift,
                Win = settings.Hotkey.Win,
                Key = VirtualKeys.Normalize(settings.Hotkey.Key)
            },
            Language = AppSettings.NormalizeLanguage(settings.Language),
            Engine = AppSettings.NormalizeEngine(settings.Engine),
            LocalModelEndpoint = settings.LocalModelEndpoint,
            LocalModelName = settings.LocalModelName,
            PreviewBeforeReplace = settings.PreviewBeforeReplace,
            CorrectParagraphWhenNothingSelected = settings.CorrectParagraphWhenNothingSelected,
            ExcludedProcesses = AppSettings.NormalizeExclusions(settings.ExcludedProcesses),
            StartWithWindows = settings.StartWithWindows,
            Logging = settings.Logging
        };
    }

    sealed class HotkeyDto
    {
        public bool Ctrl { get; set; }
        public bool Alt { get; set; }
        public bool Shift { get; set; }
        public bool Win { get; set; }
        public string? Key { get; set; }
    }
}
