using System.Net.Http;
using System.Text;
using System.Text.Json;
using LocalCorrect.Core;

namespace LocalCorrect.Engine;

/// <summary>
/// Optional engine. Posts the selection to an Ollama-compatible /api/generate on this computer only.
/// It is not used unless the user selects it. The built-in word lists stay the default.
/// </summary>
public sealed class OllamaCorrectionEngine : ICorrectionEngine, IDisposable
{
    public const string EngineId = "ollama";
    readonly Uri _uri;
    readonly string _model;
    readonly HttpClient _http;
    readonly bool _ownsHandler;

    public OllamaCorrectionEngine(string endpoint, string model, HttpMessageHandler? handler = null)
    {
        if (!LoopbackEndpoint.TryNormalize(endpoint, out var uri, out var error) || uri == null)
            throw new LocalEngineException(string.IsNullOrEmpty(error) ? "The local model address is not on this computer." : error);
        if (!IsValidModelName(model))
            throw new LocalEngineException("The local model name is not valid. Use a name such as llama3.2.");
        _uri = uri;
        _model = model.Trim();
        _ownsHandler = handler == null;
        handler ??= CreateHandler();
        _http = new HttpClient(handler, disposeHandler: _ownsHandler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public string Id => EngineId;
    public string DisplayName => "Local model on this computer (Ollama)";

    public static bool IsValidModelName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var value = name.Trim();
        if (value.Length > 80 || value.Contains("..", StringComparison.Ordinal)) return false;
        if (value.StartsWith('/') || value.EndsWith('/')) return false;
        foreach (var character in value)
        {
            if (char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or ':' or '-' or '/') continue;
            return false;
        }
        return true;
    }

    public static SocketsHttpHandler CreateHandler() => new()
    {
        UseProxy = false,
        AllowAutoRedirect = false,
        ConnectTimeout = TimeSpan.FromSeconds(3)
    };

    public CorrectionResult Correct(string text, string language)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));
        if (text.Length > 50_000)
            throw new LocalEngineException("That text is too long to send to the local model. Nothing was changed.");
        var payload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["model"] = _model,
            ["prompt"] = Prompt(text, language),
            ["stream"] = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, _uri)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        HttpResponseMessage response;
        try
        {
            response = _http.Send(request);
        }
        catch (Exception)
        {
            throw new LocalEngineException("The local model could not be reached. Nothing was changed.");
        }
        using (response)
        {
            if ((int)response.StatusCode is >= 300 and < 400)
                throw new LocalEngineException("The local model address redirected. Nothing was sent on. Nothing was changed.");
            if (!response.IsSuccessStatusCode)
                throw new LocalEngineException("The local model could not be reached. Nothing was changed.");
            var body = ReadLimited(response);
            string? corrected;
            try
            {
                using var document = JsonDocument.Parse(body);
                if (!document.RootElement.TryGetProperty("response", out var property) || property.ValueKind != JsonValueKind.String)
                    throw new LocalEngineException("The local model returned an unexpected response. Nothing was changed.");
                corrected = property.GetString();
            }
            catch (LocalEngineException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new LocalEngineException("The local model returned an unexpected response. Nothing was changed.");
            }
            corrected = Clean(corrected);
            if (string.IsNullOrEmpty(corrected))
                throw new LocalEngineException("The local model returned empty text. Nothing was changed.");
            if (corrected.Length > 100_000)
                throw new LocalEngineException("The local model returned too much text. Nothing was changed.");
            if (corrected == text)
                return new CorrectionResult(text, LanguageOf(language), Array.Empty<CorrectionEdit>());
            var edits = new[] { new CorrectionEdit(0, text.Length, text, corrected) };
            return new CorrectionResult(corrected, LanguageOf(language), edits);
        }
    }

    public void Dispose() => _http.Dispose();

    static string Prompt(string text, string? language)
    {
        var which = (language ?? "auto").Trim().ToLowerInvariant() switch
        {
            "en" => "The text is English.",
            "de" => "The text is German.",
            _ => "The text is English or German."
        };
        return "Correct spelling and obvious typos. Keep the same language, meaning, and line breaks. Return only the corrected text.\n"
            + which + "\n\n" + text;
    }

    static string LanguageOf(string? language)
    {
        var value = (language ?? "auto").Trim().ToLowerInvariant();
        return value is "en" or "de" ? value : "auto";
    }

    static string Clean(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[trimmed.Length - 1] == '"')
            trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
        return trimmed;
    }

    static string ReadLimited(HttpResponseMessage response)
    {
        using var stream = response.Content.ReadAsStream();
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        var total = 0;
        while (true)
        {
            var read = stream.Read(chunk, 0, chunk.Length);
            if (read == 0) break;
            total += read;
            if (total > 1_000_000)
                throw new LocalEngineException("The local model returned too much text. Nothing was changed.");
            buffer.Write(chunk, 0, read);
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
