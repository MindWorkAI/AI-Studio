using AIStudio.Tools.Web;

namespace AIStudio.Tests.Tools.Web;

/// <summary>
/// Checks which responses the web page retrieval reads, and whether it reads them as a page or
/// as the text of a document.
/// </summary>
/// <remarks>
/// Both directions matter. A text format classified as unreadable is what the read web page tool
/// used to fail on — plain text and JSON above all, which research runs into constantly. A binary
/// format classified as text would reach the model as noise, and an HTML page classified as text
/// would reach it as raw markup, navigation and scripts included.
/// </remarks>
[TestFixture]
public sealed class WebContentTypeClassifierTests
{
    [TestCase("text/html")]
    [TestCase("application/xhtml+xml")]
    [TestCase("TEXT/HTML")]
    [TestCase("")]
    [TestCase("  ")]
    public void PagesAreReadAsHtml(string mediaType)
    {
        Assert.That(WebContentTypeClassifier.Classify(mediaType), Is.EqualTo(WebContentKind.HTML_PAGE), "A page has to go through the HTML extraction, and a server leaving the type out is almost always serving a page.");
    }

    [TestCase("text/plain")]
    [TestCase("text/markdown")]
    [TestCase("text/csv")]
    [TestCase("text/xml")]
    [TestCase("application/json")]
    [TestCase("Application/JSON")]
    [TestCase("application/problem+json")]
    [TestCase("application/ld+json")]
    [TestCase("application/xml")]
    [TestCase("application/rss+xml")]
    [TestCase("application/atom+xml")]
    [TestCase("application/x-ndjson")]
    [TestCase("application/javascript")]
    [TestCase("application/yaml")]
    [TestCase("application/toml")]
    public void TextFormatsAreReadAsTheyStand(string mediaType)
    {
        Assert.That(WebContentTypeClassifier.Classify(mediaType), Is.EqualTo(WebContentKind.TEXT_DOCUMENT), "A text format has to be readable, and its text has to reach the model unchanged instead of being parsed as HTML.");
    }

    [TestCase("application/pdf")]
    [TestCase("application/octet-stream")]
    [TestCase("application/zip")]
    [TestCase("image/png")]
    [TestCase("image/svg+xml")]
    [TestCase("video/mp4")]
    [TestCase("audio/mpeg")]
    public void BinaryFormatsAreRefused(string mediaType)
    {
        Assert.That(WebContentTypeClassifier.Classify(mediaType), Is.Null, "Binary content decoded as text is noise to a model, and it has to be refused before its body is downloaded. The +xml suffix counts for application types only, which keeps an SVG image out.");
    }
}