using System.Net;
using System.Net.Sockets;

namespace LocalCorrect.Engine;

/// <summary>
/// Accepts only an Ollama-compatible address on this computer.
/// Anything else is rejected before a request is built, so selected text cannot leave the machine.
/// </summary>
public static class LoopbackEndpoint
{
    public static bool TryNormalize(string? input, out Uri? generateUri, out string error)
    {
        generateUri = null;
        error = "";
        if (string.IsNullOrWhiteSpace(input) || !Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri))
        {
            error = "Enter an address like http://127.0.0.1:11434.";
            return false;
        }
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            error = "The local model address must start with http:// or https://.";
            return false;
        }
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            error = "The local model address cannot include a username or password.";
            return false;
        }
        if (!IsLoopbackHost(uri.Host))
        {
            error = "The local model must run on this computer (127.0.0.1, localhost, or ::1). LocalCorrect will not send text anywhere else.";
            return false;
        }
        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) && !ResolvesToLoopback("localhost"))
        {
            error = "localhost does not resolve to this computer. Use http://127.0.0.1:11434.";
            return false;
        }
        var builder = new UriBuilder(uri)
        {
            Path = "/api/generate",
            Query = "",
            Fragment = ""
        };
        generateUri = builder.Uri;
        return true;
    }

    public static bool IsLoopbackHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        var trimmed = host.Trim().Trim('[', ']');
        return IPAddress.TryParse(trimmed, out var address) && IPAddress.IsLoopback(address);
    }

    public static bool ResolvesToLoopback(string host)
    {
        try
        {
            var addresses = Dns.GetHostAddresses(host);
            return addresses.Length > 0 && Array.TrueForAll(addresses, IPAddress.IsLoopback);
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
