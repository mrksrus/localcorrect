using System.Text;
using System.Text.RegularExpressions;

namespace LocalCorrect.Core;

/// <summary>Reads the clipboard's "HTML Format" (CF_HTML: a text header, then UTF-8 HTML).</summary>
public static partial class ClipboardHtml
{
    /// <summary>
    /// True when the copied HTML holds a link, image, table or inline formatting that a plain-text paste would remove.
    /// The whole document is scanned, not only the fragment, because editors put the formatting around a selection outside it.
    /// </summary>
    public static bool HasFormatting(byte[] data) => Tags().IsMatch(Encoding.UTF8.GetString(data));

    [GeneratedRegex(@"<(a|img|table|b|strong|i|em|u|s|sup|sub)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Tags();
}
