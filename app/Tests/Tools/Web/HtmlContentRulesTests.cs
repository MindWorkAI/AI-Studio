using AIStudio.Tools.Web;

using HtmlAgilityPack;

namespace AIStudio.Tests.Tools.Web;

/// <summary>
/// Checks the rules web pages and mails share for HTML from strangers.
/// </summary>
[TestFixture]
public sealed class HtmlContentRulesTests
{
    [TestCase("javascript:alert(1)", ExpectedResult = true)]
    [TestCase("  JavaScript:alert(1)", ExpectedResult = true)]
    [TestCase("java&#9;script:alert(1)", ExpectedResult = true)]
    [TestCase("java\nscript:alert(1)", ExpectedResult = true)]
    [TestCase("vbscript:msgbox", ExpectedResult = true)]
    [TestCase("data:text/html;base64,PHNjcmlwdD4=", ExpectedResult = true)]
    [TestCase("https://example.org/report", ExpectedResult = false)]
    [TestCase("mailto:bob@example.org", ExpectedResult = false)]
    [TestCase("#top", ExpectedResult = false)]
    public bool LinksRunningCodeAreFound(string url) => HtmlContentRules.IsScriptOrDataUrl(url);

    [TestCase("<div style='display:none'>x</div>", ExpectedResult = true)]
    [TestCase("<div style='DISPLAY : None !important'>x</div>", ExpectedResult = true)]
    [TestCase("<div style='color:red; visibility: hidden'>x</div>", ExpectedResult = true)]
    [TestCase("<div hidden>x</div>", ExpectedResult = true)]
    [TestCase("<span aria-hidden='true'>x</span>", ExpectedResult = true)]
    [TestCase("<div style='display:none; display:block'>x</div>", ExpectedResult = false)]
    [TestCase("<div style='display:block'>x</div>", ExpectedResult = false)]
    [TestCase("<div>x</div>", ExpectedResult = false)]
    public bool HiddenElementsAreFound(string html) => HtmlContentRules.IsHiddenByMarkup(HtmlNode.CreateNode(html));
}