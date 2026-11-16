using System.Net.Http;
using LocalCorrect.Core;

namespace LocalCorrect.Engine;

public static class CorrectionEngineFactory
{
    /// <summary>
    /// Builds the engine the user picked. A local-model address that is not on this
    /// computer is refused and the built-in word lists are used instead.
    /// </summary>
    public static ICorrectionEngine Create(AppSettings settings, SymSpellCorrector symspell, HttpMessageHandler? httpHandler, out string? warning)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(symspell);
        warning = null;
        if (!string.Equals(settings.Engine, OllamaCorrectionEngine.EngineId, StringComparison.OrdinalIgnoreCase))
            return symspell;
        try
        {
            return new OllamaCorrectionEngine(settings.LocalModelEndpoint, settings.LocalModelName, httpHandler);
        }
        catch (LocalEngineException ex)
        {
            warning = ex.Message;
            return symspell;
        }
    }
}
