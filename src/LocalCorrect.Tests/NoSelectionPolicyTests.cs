using System.Text;
using LocalCorrect.Core;

namespace LocalCorrect.Tests;

public class NoSelectionPolicyTests
{
    [Theory]
    [InlineData("_WwG", SelectKeys.Paragraph)]
    [InlineData("RICHEDIT50W", SelectKeys.Paragraph)]
    [InlineData("RichEdit20W", SelectKeys.Paragraph)]
    [InlineData("RichEditD2DPT", SelectKeys.Paragraph)]
    [InlineData("WindowsForms10.RichEdit20W.app.0.2bf8098_r6_ad1", SelectKeys.Paragraph)]
    [InlineData("Chrome_RenderWidgetHostHWND", SelectKeys.Paragraph)]
    [InlineData("MozillaWindowClass", SelectKeys.Paragraph)]
    [InlineData("Edit", SelectKeys.Line)]
    [InlineData("WindowsForms10.EDIT.app.0.2bf8098_r6_ad1", SelectKeys.Line)]
    [InlineData("", SelectKeys.Line)]
    [InlineData(null, SelectKeys.Line)]
    public void KeysFollowTheEditorFamily(string? windowClass, SelectKeys expected) =>
        Assert.Equal(expected, NoSelectionPolicy.KeysFor(windowClass));

    [Theory]
    [InlineData("Text\r\n", "Text", true)]
    [InlineData("Text\r", "Text", true)]
    [InlineData("Text\n", "Text", true)]
    [InlineData("Text\r\n\r\n", "Text\r\n", true)]
    [InlineData("Text", "Text", false)]
    [InlineData("", "", false)]
    public void OneTrailingBreakIsDropped(string input, string expected, bool dropped)
    {
        var text = input;
        Assert.Equal(dropped, NoSelectionPolicy.DropTrailingBreak(ref text));
        Assert.Equal(expected, text);
    }

    [Theory]
    [InlineData("A plain sentence, with punctuation; and \"quotes\".", false)]
    [InlineData("Two\r\nlines", true)]
    [InlineData("Two\nlines", true)]
    [InlineData("Shift\vEnter", true)]
    [InlineData("Page\fbreak", true)]
    [InlineData("Tab\tbed", true)]
    [InlineData("Cell\amark", true)]
    public void OnlyASingleRunOfTextIsAccepted(string text, bool rejected) =>
        Assert.Equal(rejected, NoSelectionPolicy.Reject(text, null) != null);

    [Fact]
    public void LengthLimitIsFiveThousand()
    {
        Assert.Null(NoSelectionPolicy.Reject(new string('a', 5000), null));
        Assert.NotNull(NoSelectionPolicy.Reject(new string('a', 5001), null));
    }

    [Fact]
    public void FormattedHtmlIsRejected()
    {
        Assert.NotNull(NoSelectionPolicy.Reject("text", Encoding.UTF8.GetBytes("<i>text</i>")));
        Assert.Null(NoSelectionPolicy.Reject("text", Encoding.UTF8.GetBytes("<p>text</p>")));
    }

    [Theory]
    [InlineData("<a href=\"x\">link</a>")]
    [InlineData("<A HREF=x>link</A>")]
    [InlineData("<img src=x>")]
    [InlineData("<table><tr><td>x</td></tr></table>")]
    [InlineData("<b>bold</b>")]
    [InlineData("<b style='mso-bidi-font-weight:normal'>bold</b>")]
    [InlineData("<strong>bold</strong>")]
    [InlineData("<i>it</i>")]
    [InlineData("<em>it</em>")]
    [InlineData("<u>under</u>")]
    [InlineData("<s>struck</s>")]
    [InlineData("<sup>2</sup>")]
    [InlineData("<sub>2</sub>")]
    public void FormattingTagsAreFound(string fragment) =>
        Assert.True(ClipboardHtml.HasFormatting(CfHtml(fragment)));

    [Theory]
    [InlineData("<p>plain</p>")]
    [InlineData("<span style=\"color:red\">plain</span>")]
    [InlineData("<div>line<br>two</div>")]
    [InlineData("<ul><li>item</li></ul>")]
    [InlineData("<script>x</script><style>a:link{}</style>")]
    [InlineData("<input value=\"a\"><abbr>x</abbr><small>s</small>")]
    [InlineData("<o:p></o:p><blockquote>q</blockquote>")]
    [InlineData("text with a < b and i<j")]
    public void PlainMarkupIsNotFormatting(string fragment) =>
        Assert.False(ClipboardHtml.HasFormatting(CfHtml(fragment)));

    /// <summary>A CF_HTML block as Windows programs write it: header, then the document, UTF-8.</summary>
    static byte[] CfHtml(string fragment)
    {
        var body = "<html><head><title>t</title><meta charset=utf-8></head><body><!--StartFragment-->" + fragment + "<!--EndFragment--></body></html>";
        var header = "Version:0.9\r\nStartHTML:0000000105\r\nEndHTML:0000000000\r\nStartFragment:0000000000\r\nEndFragment:0000000000\r\nSourceURL:https://example.com/\r\n";
        return Encoding.UTF8.GetBytes(header + body + "\0");
    }
}
