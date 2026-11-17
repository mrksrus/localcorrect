using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace LocalCorrect.Engine;

public enum LanguageToolState { NotInstalled, Starting, Ready, Failed, Stopped }

/// <summary>A started server process: its exit and a way to end it with its children.</summary>
public interface IServerProcess
{
    Task Exited { get; }
    void Kill();
}

/// <summary>
/// Runs the bundled LanguageTool server on 127.0.0.1: picks a free port, starts it from the app folder,
/// waits until it answers, warms up German and English, restarts it once after a crash and stops it.
/// <see cref="StateChanged"/> is raised on a background thread and must not block.
/// </summary>
public sealed class LanguageToolServer : IDisposable
{
    // Synthetic text with errors: the speller loads suggestion data on first use (a clean sentence left the first real
    // German check at 9 s, a misspelt verb form such as "zitirte" alone cost 3.7 s). The parameters are those of every
    // real request: the server's pipeline cache is keyed by them.
    static readonly (string Language, string Text)[] WarmUp =
    {
        ("de-DE", "Hallo Frau Beispiel,\nich habe heute keine zeit, aber morgen könen wir uns treffen. Ich glaube das wir das Aufjedenfall schaffen.\n"
            + "> Die Rechnug ist angekomen.\nEr hat das zitirte Formular unterschriben. Ich weis nicht, ob das klappt. Sag mir bitte bescheid."),
        ("en-US", "Hello Sam,\ni will recieve the parcel tommorow and we have alot of work.\n> Its a nice day.\nTheir going to the meeting, I think.")
    };

    readonly object _gate = new();
    readonly string _appDirectory;
    readonly Func<ProcessStartInfo, IServerProcess> _launch;
    int _attempt;
    bool _restarted;
    DateTime _attemptStarted;
    IServerProcess? _process;

    public LanguageToolServer(string appDirectory, Action<Process>? onStarted = null,
        Func<ProcessStartInfo, IServerProcess>? launcher = null, HttpMessageHandler? handler = null)
    {
        _appDirectory = appDirectory ?? throw new ArgumentNullException(nameof(appDirectory));
        _launch = launcher ?? (info => RealProcess.Start(info, onStarted));
        Http = new HttpClient(handler ?? OllamaCorrectionEngine.CreateHandler()) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
    }

    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan ListenTimeout { get; init; } = TimeSpan.FromSeconds(90);
    public TimeSpan WarmUpTimeout { get; init; } = TimeSpan.FromSeconds(180);
    public TimeSpan RestartWindow { get; init; } = TimeSpan.FromSeconds(60);

    public LanguageToolState State { get; private set; } = LanguageToolState.Stopped;
    /// <summary>Base address such as http://127.0.0.1:51234/v2/ while <see cref="LanguageToolState.Ready"/>.</summary>
    public Uri? Address { get; private set; }
    /// <summary>Counts the server starts, restarts included, so a notice can be shown once per start.</summary>
    public int Starts { get; private set; }
    public TimeSpan ListenTime { get; private set; }
    public TimeSpan WarmUpTime { get; private set; }
    /// <summary>Shared client without a timeout of its own: each caller passes a cancellation deadline.</summary>
    public HttpClient Http { get; }
    public event Action<LanguageToolState>? StateChanged;

    string ToolDirectory => Path.Combine(_appDirectory, "languagetool");
    string JavaPath => Path.Combine(_appDirectory, "jre", "bin", OperatingSystem.IsWindows() ? "java.exe" : "java");

    public static FormUrlEncodedContent CheckContent(string language, string text) =>
        new(new[] { KeyValuePair.Create("language", language), KeyValuePair.Create("text", text) });

    /// <summary>Starts the server unless it is starting or running. Also leaves <see cref="LanguageToolState.Failed"/>.</summary>
    public void Start()
    {
        lock (_gate)
        {
            if (State is LanguageToolState.Starting or LanguageToolState.Ready) return;
            if (!File.Exists(JavaPath) || !File.Exists(Path.Combine(ToolDirectory, "languagetool-server.jar")))
            {
                Set(LanguageToolState.NotInstalled);
                return;
            }
            _restarted = false;
            Launch();
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _attempt++;
            KillProcess();
            if (State != LanguageToolState.NotInstalled) Set(LanguageToolState.Stopped);
        }
    }

    public void Dispose()
    {
        Stop();
        Http.Dispose();
    }

    void Launch()
    {
        var attempt = ++_attempt;
        Starts++;
        Address = null;
        _attemptStarted = DateTime.UtcNow;
        Set(LanguageToolState.Starting);
        var port = FreePort();
        // Relative paths only, so a user folder with spaces or umlauts never appears in the arguments.
        var info = new ProcessStartInfo(JavaPath)
        {
            WorkingDirectory = ToolDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
            // Output is not redirected: a pipe nobody reads would block the server.
        };
        foreach (var argument in new[]
        {
            "-Xmx768m", "-Dfile.encoding=UTF-8", "-Djava.net.preferIPv4Stack=true", "-cp", "languagetool-server.jar",
            "org.languagetool.server.HTTPServer", "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--config", Path.Combine("..", "languagetool-server.properties")
        }) info.ArgumentList.Add(argument);

        IServerProcess process;
        try
        {
            process = _launch(info);
        }
        catch (Exception)
        {
            Fail(attempt);
            return;
        }
        _process = process;
        process.Exited.ContinueWith(_ => { lock (_gate) Fail(attempt); }, TaskScheduler.Default);
        var address = new Uri("http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/v2/");
        Task.Run(() => BringUp(attempt, process, address));
    }

    async Task BringUp(int attempt, IServerProcess process, Uri address)
    {
        var clock = Stopwatch.StartNew();
        var listening = false;
        while (!listening && clock.Elapsed < ListenTimeout && !process.Exited.IsCompleted && attempt == Volatile.Read(ref _attempt))
        {
            listening = await Answers(() => new HttpRequestMessage(HttpMethod.Get, new Uri(address, "languages")), TimeSpan.FromSeconds(2));
            if (!listening) await Task.Delay(PollInterval);
        }
        var listenTime = clock.Elapsed;
        var warm = listening;
        foreach (var (language, text) in WarmUp)
        {
            if (!warm) break;
            warm = await Answers(() => new HttpRequestMessage(HttpMethod.Post, new Uri(address, "check")) { Content = CheckContent(language, text) }, WarmUpTimeout);
        }
        lock (_gate)
        {
            if (attempt != _attempt) return;
            if (!warm)
            {
                Fail(attempt);
                return;
            }
            ListenTime = listenTime;
            WarmUpTime = clock.Elapsed - listenTime;
            Address = address;
            Set(LanguageToolState.Ready);
        }
    }

    async Task<bool> Answers(Func<HttpRequestMessage> request, TimeSpan timeout)
    {
        try
        {
            using var cancel = new CancellationTokenSource(timeout);
            using var message = request();
            using var response = await Http.SendAsync(message, cancel.Token);
            return response.StatusCode == HttpStatusCode.OK;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // Called under _gate when an attempt exits, does not answer or cannot start.
    void Fail(int attempt)
    {
        if (attempt != _attempt || State is not (LanguageToolState.Starting or LanguageToolState.Ready)) return;
        var ranLong = State == LanguageToolState.Ready && DateTime.UtcNow - _attemptStarted > RestartWindow;
        KillProcess();
        if (ranLong) _restarted = false;
        if (_restarted)
        {
            Address = null;
            Set(LanguageToolState.Failed);
            return;
        }
        _restarted = true;
        Launch();
    }

    void KillProcess()
    {
        Address = null;
        try
        {
            _process?.Kill();
        }
        catch (Exception)
        {
            // Already gone.
        }
        _process = null;
    }

    void Set(LanguageToolState state)
    {
        if (State == state) return;
        State = state;
        StateChanged?.Invoke(state);
    }

    static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    sealed class RealProcess : IServerProcess
    {
        readonly Process _process;

        RealProcess(Process process)
        {
            _process = process;
            Exited = process.WaitForExitAsync();
        }

        public Task Exited { get; }

        public static IServerProcess Start(ProcessStartInfo info, Action<Process>? onStarted)
        {
            var process = Process.Start(info) ?? throw new InvalidOperationException("java did not start");
            try
            {
                onStarted?.Invoke(process);
            }
            catch (Exception)
            {
                // Without the job object the server is still stopped on Exit; only a crash of this app could leave it.
            }
            return new RealProcess(process);
        }

        public void Kill() => _process.Kill(entireProcessTree: true);
    }
}
