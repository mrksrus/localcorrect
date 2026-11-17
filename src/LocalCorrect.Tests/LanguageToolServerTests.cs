using LocalCorrect.Engine;

namespace LocalCorrect.Tests;

public class LanguageToolServerTests
{
    [Fact]
    public void StartsWithRelativePathsThenWarmsUpGermanAndEnglish()
    {
        var fake = new FakeLanguageTool();
        using var server = fake.Server();
        var states = new List<LanguageToolState>();
        server.StateChanged += state => { lock (states) states.Add(state); };
        server.Start();
        FakeLanguageTool.WaitFor(() => server.State == LanguageToolState.Ready);

        var info = Assert.Single(fake.Launches);
        Assert.EndsWith("languagetool", info.WorkingDirectory, StringComparison.Ordinal);
        Assert.False(info.RedirectStandardOutput || info.RedirectStandardError || info.UseShellExecute);
        Assert.True(info.CreateNoWindow);
        var args = info.ArgumentList.ToList();
        Assert.Equal(Path.Combine("..", "languagetool-server.properties"), args[args.IndexOf("--config") + 1]);
        Assert.DoesNotContain("--public", args);
        Assert.DoesNotContain(args, arg => Path.IsPathRooted(arg));
        var port = args[args.IndexOf("--port") + 1];
        Assert.Equal("http://127.0.0.1:" + port + "/v2/", server.Address!.ToString());

        var checks = fake.Requests.Where(request => request.StartsWith("/v2/check", StringComparison.Ordinal)).ToList();
        Assert.StartsWith("/v2/languages", fake.Requests[0], StringComparison.Ordinal);
        Assert.Equal(2, checks.Count);
        Assert.Contains("language=de-DE", checks[0], StringComparison.Ordinal);
        Assert.Contains("language=en-US", checks[1], StringComparison.Ordinal);
        Assert.Equal(new[] { LanguageToolState.Starting, LanguageToolState.Ready }, states);
        Assert.Equal(1, server.Starts);
    }

    [Fact]
    public void ACrashRestartsOnceAndASecondCrashSoonAfterFails()
    {
        var fake = new FakeLanguageTool();
        using var server = fake.Server();
        server.Start();
        FakeLanguageTool.WaitFor(() => server.State == LanguageToolState.Ready);

        fake.Processes[0].Crash();
        FakeLanguageTool.WaitFor(() => server.Starts == 2 && server.State == LanguageToolState.Ready);
        Assert.Equal(2, fake.Launches.Count);

        fake.Processes[1].Crash();
        FakeLanguageTool.WaitFor(() => server.State == LanguageToolState.Failed);
        Thread.Sleep(100);
        Assert.Equal(2, fake.Launches.Count);
        Assert.Null(server.Address);

        // A settings change calls Start again, which leaves Failed.
        server.Start();
        FakeLanguageTool.WaitFor(() => server.State == LanguageToolState.Ready);
        Assert.Equal(3, fake.Launches.Count);
    }

    [Fact]
    public void AServerThatNeverAnswersIsTriedTwiceThenFails()
    {
        var fake = new FakeLanguageTool { Listening = false };
        using var server = fake.Server(listenTimeout: TimeSpan.FromMilliseconds(200));
        server.Start();
        FakeLanguageTool.WaitFor(() => server.State == LanguageToolState.Failed);
        Assert.Equal(2, fake.Launches.Count);
        Assert.All(fake.Processes, process => Assert.True(process.Killed));
    }

    [Fact]
    public void AFailedWarmUpCountsAsAFailedStart()
    {
        var fake = new FakeLanguageTool { Check = _ => new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError) };
        using var server = fake.Server();
        server.Start();
        FakeLanguageTool.WaitFor(() => server.State == LanguageToolState.Failed);
        Assert.Equal(2, fake.Launches.Count);
    }

    [Fact]
    public void StopKillsTheProcessAndDoesNotRestart()
    {
        var fake = new FakeLanguageTool();
        using var server = fake.Server();
        server.Start();
        FakeLanguageTool.WaitFor(() => server.State == LanguageToolState.Ready);
        server.Stop();
        Assert.Equal(LanguageToolState.Stopped, server.State);
        Assert.True(fake.Processes[0].Killed);
        Assert.Null(server.Address);
        Thread.Sleep(100);
        Assert.Single(fake.Launches);
    }

    [Fact]
    public void AMissingRuntimeMeansNotInstalled()
    {
        var fake = new FakeLanguageTool();
        using var server = fake.Server(FakeLanguageTool.Install(complete: false));
        server.Start();
        Assert.Equal(LanguageToolState.NotInstalled, server.State);
        Assert.Empty(fake.Launches);
    }
}
