using System.Net;
using System.Net.Http;
using LocalCorrect.Core;
using LocalCorrect.Engine;

namespace LocalCorrect.Tests;

public class SettingsAndLogTests
{
    [Fact]
    public void StartupLineIsWrittenWithNoOtherSetup()
    {
        var path = Path.Combine(Path.GetTempPath(), "localcorrect-tests", Guid.NewGuid().ToString("n"), "log.txt");
        StartupLog.WriteFirstLine(path);
        StartupLog.WriteFirstLine(path);
        var text = File.ReadAllText(path);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Contains(" start pid=", lines[0], StringComparison.Ordinal);
        StartupLog.WriteFirstLine("");
        StartupLog.WriteFirstLine(Path.Combine(path, "not-a-directory"));
    }

    [Fact]
    public void FileLogSkipsInfoWhenDisabledAndAlwaysWritesErrors()
    {
        var path = Path.Combine(Path.GetTempPath(), "localcorrect-tests", Guid.NewGuid().ToString("n"), "log.txt");
        FileLog.Write(path, loggingEnabled: false, "info", "hidden\nsecond");
        FileLog.Write(path, loggingEnabled: false, "error", "visible");
        FileLog.Write(path, loggingEnabled: true, "info", "shown");
        var text = File.ReadAllText(path);
        Assert.DoesNotContain("hidden", text, StringComparison.Ordinal);
        Assert.Contains(" error visible", text, StringComparison.Ordinal);
        Assert.Contains(" info shown", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\nsecond", text, StringComparison.Ordinal);
    }

    [Fact]
    public void PathsStayUnderThePerUserFolders()
    {
        Assert.Equal(Path.Combine("roam", "LocalCorrect", "settings.json"), AppPaths.SettingsFile("roam"));
        Assert.Equal(Path.Combine("local", "LocalCorrect", "log.txt"), AppPaths.LogFile("local"));
        var installed = AppPaths.InstalledExe("local");
        Assert.True(AppPaths.IsInstalledLocation(installed, "local"));
        Assert.False(AppPaths.IsInstalledLocation(Path.Combine("downloads", "LocalCorrect.exe"), "local"));
    }

    [Fact]
    public void DefaultHotkeyIsCtrlAltL()
    {
        var hotkey = new HotkeySpec();
        Assert.True(hotkey.IsValid);
        Assert.Equal("Ctrl+Alt+L", hotkey.Display);
        Assert.Equal(3u, hotkey.NativeModifiers);
        Assert.True(VirtualKeys.TryGet("F1", out var f1));
        Assert.Equal(0x70, f1);
        Assert.False(new HotkeySpec { Ctrl = false, Alt = false, Shift = true, Key = "L" }.IsValid);
        Assert.False(new HotkeySpec { Ctrl = false, Alt = true, Key = "F4" }.IsValid);
        Assert.False(new HotkeySpec { Ctrl = false, Alt = false, Win = true, Key = "E" }.IsValid);
    }

    [Fact]
    public void SettingsRoundTripAndCorruptFilesFallBack()
    {
        var path = Path.Combine(Path.GetTempPath(), "localcorrect-tests", Guid.NewGuid().ToString("n"), "settings.json");
        var settings = new AppSettings
        {
            Hotkey = new HotkeySpec { Ctrl = true, Alt = false, Shift = true, Key = "k" },
            Language = "DE",
            Engine = "ollama",
            LocalModelEndpoint = "http://127.0.0.1:11434",
            LocalModelName = "llama3.2",
            PreviewBeforeReplace = false,
            ExcludedProcesses = new List<string> { "Outlook.exe", "outlook", "  keepass  " },
            StartWithWindows = true,
            Logging = true
        };
        SettingsStore.Save(path, settings);
        var loaded = SettingsStore.Load(path);
        Assert.Equal("Ctrl+Shift+K", loaded.Hotkey.Display);
        Assert.Equal("de", loaded.Language);
        Assert.Equal("ollama", loaded.Engine);
        Assert.False(loaded.PreviewBeforeReplace);
        Assert.True(loaded.StartWithWindows);
        Assert.True(loaded.Logging);
        Assert.Equal(new[] { "Outlook", "keepass" }, loaded.ExcludedProcesses);

        File.WriteAllText(path, "{ this is not json");
        var fallback = SettingsStore.Load(path);
        Assert.Equal("auto", fallback.Language);
        Assert.Equal("Ctrl+Alt+L", fallback.Hotkey.Display);
        Assert.Equal("languagetool", SettingsStore.Load(path + ".missing").Engine);
    }

    [Theory]
    [InlineData("symspell", "languagetool")]
    [InlineData("SymSpell", "languagetool")]
    [InlineData("wordlists", "wordlists")]
    [InlineData("ollama", "ollama")]
    [InlineData("languagetool", "languagetool")]
    [InlineData("something-else", "languagetool")]
    public void SavedEngineIdsAreMigrated(string saved, string expected)
    {
        var path = Path.Combine(Path.GetTempPath(), "localcorrect-tests", Guid.NewGuid().ToString("n"), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{\"engine\":\"" + saved + "\",\"language\":\"de\"}");
        var loaded = SettingsStore.Load(path);
        Assert.Equal(expected, loaded.Engine);
        Assert.Equal("de", loaded.Language);
        SettingsStore.Save(path, loaded);
        Assert.Equal(expected, SettingsStore.Load(path).Engine);
    }

    [Fact]
    public void InvalidHotkeyInTheFileUsesTheDefault()
    {
        var path = Path.Combine(Path.GetTempPath(), "localcorrect-tests", Guid.NewGuid().ToString("n"), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{"hotkey":{"ctrl":false,"alt":false,"shift":true,"win":false,"key":"L"},"language":"fr"}""");
        var loaded = SettingsStore.Load(path);
        Assert.Equal("Ctrl+Alt+L", loaded.Hotkey.Display);
        Assert.Equal("auto", loaded.Language);
    }

    [Fact]
    public void ExclusionMatchIsExactAndIgnoresExe()
    {
        var list = new[] { "outlook", "1Password.exe" };
        Assert.True(ProcessFilter.IsExcluded(@"C:\Program Files\Microsoft Office\root\Office16\OUTLOOK.EXE", list));
        Assert.True(ProcessFilter.IsExcluded("1password", list));
        Assert.False(ProcessFilter.IsExcluded("explorer", list));
        Assert.False(ProcessFilter.IsExcluded("winword", list));
        Assert.False(ProcessFilter.IsExcluded(null, list));
    }

    [Theory]
    [InlineData("http://127.0.0.1:11434", true)]
    [InlineData("http://127.0.0.1:11434/api/generate", true)]
    [InlineData("https://localhost:11434/", true)]
    [InlineData("http://[::1]:11434", true)]
    [InlineData("http://example.com:11434", false)]
    [InlineData("http://8.8.8.8", false)]
    [InlineData("http://127.0.0.1.evil.com", false)]
    [InlineData("http://user:pass@127.0.0.1:11434", false)]
    [InlineData("file:///tmp/model", false)]
    [InlineData("", false)]
    public void LocalModelAddressMustStayOnThisComputer(string input, bool allowed)
    {
        var ok = LoopbackEndpoint.TryNormalize(input, out var uri, out var error);
        Assert.Equal(allowed, ok);
        if (allowed)
        {
            Assert.NotNull(uri);
            Assert.EndsWith("/api/generate", uri!.AbsolutePath, StringComparison.Ordinal);
            Assert.True(LoopbackEndpoint.IsLoopbackHost(uri.Host));
        }
        else
        {
            Assert.False(string.IsNullOrEmpty(error));
        }
    }

    [Fact]
    public void OllamaEngineDoesNotCallOutForAPublicAddress()
    {
        var called = false;
        var handler = new StubHandler(_ =>
        {
            called = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var settings = new AppSettings { Engine = "ollama", LocalModelEndpoint = "http://example.com", LocalModelName = "llama3.2" };
        var symspell = new SymSpellCorrector("the 10\n", "und 10\n");
        var engine = CorrectionEngineFactory.Create(settings, symspell, null, handler, out var warning);
        Assert.Same(symspell, engine);
        Assert.False(string.IsNullOrEmpty(warning));
        Assert.False(called);
        Assert.False(OllamaCorrectionEngine.CreateHandler().UseProxy);
        Assert.False(OllamaCorrectionEngine.CreateHandler().AllowAutoRedirect);
    }

    [Fact]
    public void OllamaEnginePostsOnlyToLoopbackAndUsesTheResponse()
    {
        var handler = new StubHandler(request =>
        {
            Assert.Equal("127.0.0.1", request.RequestUri!.Host);
            Assert.EndsWith("/api/generate", request.RequestUri.AbsolutePath, StringComparison.Ordinal);
            using var reader = new StreamReader(request.Content!.ReadAsStream());
            var body = reader.ReadToEnd();
            Assert.Contains("recieve", body, StringComparison.Ordinal);
            Assert.DoesNotContain("example.com", body, StringComparison.Ordinal);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"response":" \"receive\" "}""")
            };
        });
        using var engine = new OllamaCorrectionEngine("http://127.0.0.1:11434", "llama3.2", handler);
        var result = engine.Correct("recieve", "en");
        Assert.Equal("receive", result.Text);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public void OllamaAsksForTemperatureZeroAndKeepsTheModelLoaded()
    {
        string? body = null;
        var handler = new StubHandler(request =>
        {
            using var reader = new StreamReader(request.Content!.ReadAsStream());
            body = reader.ReadToEnd();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"response":"hello"}""") };
        });
        using var engine = new OllamaCorrectionEngine("http://127.0.0.1:11434", "llama3.2", handler);
        engine.Correct("hello", "en");
        using var json = System.Text.Json.JsonDocument.Parse(body!);
        Assert.Equal(0, json.RootElement.GetProperty("options").GetProperty("temperature").GetInt32());
        Assert.Equal("30m", json.RootElement.GetProperty("keep_alive").GetString());
        Assert.False(json.RootElement.GetProperty("stream").GetBoolean());
    }

    [Theory]
    [InlineData("I will recieve teh parcel tomorow.", "I will receive the parcel tomorrow.", false)]
    [InlineData("dont", "don't", false)]
    [InlineData("Line one has a typo.\nLine two.", "Line one has a typo. Line two.", true)]
    [InlineData("Please send the report by Friday.", "Kindly ensure that the quarterly report reaches me before Friday.", true)]
    [InlineData("Please send the report by Friday morning.", "Please mail this summary until Monday evening.", true)]
    public void OllamaRewriteGuard(string text, string answer, bool rejected)
    {
        Assert.Equal(rejected, OllamaCorrectionEngine.IsRewrite(text, answer));
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new { response = answer }))
        });
        using var engine = new OllamaCorrectionEngine("http://127.0.0.1:11434", "llama3.2", handler);
        if (!rejected)
        {
            Assert.Equal(answer, engine.Correct(text, "en").Text);
            return;
        }
        var error = Assert.Throws<LocalEngineException>(() => engine.Correct(text, "en"));
        Assert.Equal("The local model changed too much of the text. Nothing was changed.", error.Message);
    }

    [Fact]
    public void FactoryPicksLanguageToolByDefault()
    {
        var symspell = new SymSpellCorrector("the 10\n", "und 10\n");
        using var server = new LanguageToolServer(Path.GetTempPath());
        var languageTool = new LanguageToolEngine(server, symspell);
        Assert.Same(languageTool, CorrectionEngineFactory.Create(new AppSettings(), symspell, languageTool, null, out _));
        Assert.Same(symspell, CorrectionEngineFactory.Create(new AppSettings { Engine = "wordlists" }, symspell, languageTool, null, out _));
    }

    [Fact]
    public void OllamaRedirectIsNotFollowed()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Redirect)
        {
            Headers = { Location = new Uri("http://example.com/steal") }
        });
        using var engine = new OllamaCorrectionEngine("http://127.0.0.1:11434", "llama3.2", handler);
        var error = Assert.Throws<LocalEngineException>(() => engine.Correct("hello", "en"));
        Assert.Contains("redirect", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, handler.Calls);
    }

    sealed class StubHandler : HttpMessageHandler
    {
        readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public int Calls;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return _respond(request);
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Send(request, cancellationToken));
    }
}
