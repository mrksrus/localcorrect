using System.Diagnostics;
using System.Net;
using System.Net.Http;
using LocalCorrect.Engine;

namespace LocalCorrect.Tests;

/// <summary>A fake LanguageTool install, process and HTTP server for the engine and server tests.</summary>
internal sealed class FakeLanguageTool : HttpMessageHandler
{
    public readonly List<string> Requests = new();
    public readonly List<ProcessStartInfo> Launches = new();
    public readonly List<FakeProcess> Processes = new();
    public bool Listening = true;
    public TimeSpan CheckDelay = TimeSpan.Zero;
    public Func<string, HttpResponseMessage> Check = _ => Json("""{"matches":[]}""");

    public static string Install(bool complete = true)
    {
        var root = Path.Combine(Path.GetTempPath(), "localcorrect-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(Path.Combine(root, "languagetool"));
        Directory.CreateDirectory(Path.Combine(root, "jre", "bin"));
        File.WriteAllText(Path.Combine(root, "languagetool", "languagetool-server.jar"), "");
        if (complete) File.WriteAllText(Path.Combine(root, "jre", "bin", OperatingSystem.IsWindows() ? "java.exe" : "java"), "");
        return root;
    }

    public LanguageToolServer Server(string? root = null, TimeSpan? listenTimeout = null) =>
        new(root ?? Install(), launcher: info =>
        {
            lock (Launches)
            {
                Launches.Add(info);
                var process = new FakeProcess();
                Processes.Add(process);
                return process;
            }
        }, handler: this)
        {
            PollInterval = TimeSpan.FromMilliseconds(10),
            ListenTimeout = listenTimeout ?? TimeSpan.FromSeconds(10)
        };

    public static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    public static void WaitFor(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException("condition not reached");
            Thread.Sleep(5);
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        var body = request.Content == null ? "" : Uri.UnescapeDataString((await request.Content.ReadAsStringAsync(cancellationToken)).Replace('+', ' '));
        lock (Requests) Requests.Add(path + " " + body);
        if (!Listening) throw new HttpRequestException("connection refused");
        if (path.EndsWith("/languages", StringComparison.Ordinal)) return Json("[]");
        await Task.Delay(CheckDelay, cancellationToken);
        return Check(body);
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
        SendAsync(request, cancellationToken).GetAwaiter().GetResult();
}

internal sealed class FakeProcess : IServerProcess
{
    readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool Killed;
    public Task Exited => _exit.Task;
    public void Crash() => _exit.TrySetResult();
    public void Kill()
    {
        Killed = true;
        _exit.TrySetResult();
    }
}
