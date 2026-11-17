using System.Diagnostics;
using LocalCorrect.Engine;
using Xunit.Abstractions;

namespace LocalCorrect.Tests;

/// <summary>
/// Runs only when LOCALCORRECT_BUNDLE_DIR names a folder with languagetool\, jre\ and the server config
/// (CI sets it after the bundle step). Starts the real server and corrects the recorded fixtures.
/// </summary>
public class BundleIntegrationTests
{
    readonly ITestOutputHelper _output;

    public BundleIntegrationTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void BundledServerCorrectsGermanAndEnglishQuickly()
    {
        var bundle = Environment.GetEnvironmentVariable("LOCALCORRECT_BUNDLE_DIR");
        if (string.IsNullOrEmpty(bundle)) return;
        var wordLists = SymSpellCorrector.FromEmbeddedLexicons();
        using var server = new LanguageToolServer(Path.GetFullPath(bundle));
        var clock = Stopwatch.StartNew();
        server.Start();
        while (server.State is LanguageToolState.Starting && clock.Elapsed < TimeSpan.FromMinutes(6)) Thread.Sleep(100);
        Assert.Equal(LanguageToolState.Ready, server.State);
        Assert.Equal(1, server.Starts);
        Log($"listening after {server.ListenTime.TotalSeconds:0.0} s, warm-up {server.WarmUpTime.TotalSeconds:0.0} s");

        var engine = new LanguageToolEngine(server, wordLists);
        foreach (var (name, expected) in new[] { ("de", new[] { "Zeit", "können", "dass", "abschließen" }), ("en", new[] { "receive", "a lot", "It's" }) })
        {
            var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "lt-" + name + ".txt"));
            var timings = new List<double>();
            for (var run = 0; run < 5; run++)
            {
                var watch = Stopwatch.StartNew();
                var result = engine.Correct(text, "auto");
                timings.Add(watch.Elapsed.TotalSeconds);
                Assert.Equal(LanguageToolEngine.EngineId, result.Engine);
                Assert.Equal(name, result.Language);
                foreach (var word in expected) Assert.Contains(word, result.Text, StringComparison.Ordinal);
            }
            Log($"{name} warm checks: {string.Join(", ", timings.Select(t => t.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)))} s");
            // Catches a lost pipeline cache (2 to 3 s per German request plus the rest), not the 1 s goal.
            if (name == "de") Assert.All(timings, t => Assert.True(t < 5, $"warm German check took {t:0.00} s"));
        }
    }

    void Log(string line)
    {
        _output.WriteLine(line);
        Console.WriteLine("bundle-integration: " + line);
        var summary = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
        if (!string.IsNullOrEmpty(summary)) File.AppendAllText(summary, "- " + line + "\n");
    }
}
