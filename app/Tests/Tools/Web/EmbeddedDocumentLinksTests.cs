using AIStudio.Tools.Web;

using HtmlAgilityPack;

namespace AIStudio.Tests.Tools.Web;

/// <summary>
/// Checks that a document which a page embeds reaches the model as a link.
/// </summary>
/// <remarks>
/// An intranet page often shows its forms and guidelines in a viewer frame. The extraction removes
/// every frame, so without a link, the model would neither learn of the document nor be allowed to
/// open it while the free address choice is off.
/// </remarks>
[TestFixture]
public sealed class EmbeddedDocumentLinksTests
{
    private static readonly Uri PAGE_URL = new("https://intranet.example.org/sites/hr/page.aspx");

    private static string ExtractMarkdown(string body) =>
        WebPageContentExtractor.Extract(Parse($"<html><body><main><h1>Travel</h1><p>Read the guideline below.</p>{body}</main></body></html>"), PAGE_URL).Markdown;

    private static HtmlDocument Parse(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        return document;
    }

    [Test]
    public void AFramedPdfBecomesALink()
    {
        var markdown = ExtractMarkdown("""<iframe src="/files/travel%20guideline.pdf" title="Travel guideline"></iframe>""");
        Assert.That(markdown, Does.Contain("[Embedded document: Travel guideline](https://intranet.example.org/files/travel%20guideline.pdf)"), "The frame names the document, and its address is made absolute like every other link.");
    }

    [Test]
    public void AFileNameWithSpacesStaysOneAddress()
    {
        var markdown = ExtractMarkdown("""<iframe src="/sites/hr/Shared Documents/Jobticket Antrag.pdf"></iframe>""");
        Assert.That(markdown, Does.Contain("(https://intranet.example.org/sites/hr/Shared%20Documents/Jobticket%20Antrag.pdf)"), "An address with a space would end at it and not count as given to the model.");
    }

    [Test]
    public void AnObjectWithADocumentTypeBecomesALinkNamedAfterItsFile()
    {
        var markdown = ExtractMarkdown("""<object data="download.aspx?id=42" type="application/pdf"></object>""");
        Assert.That(markdown, Does.Contain("[Embedded document: download.aspx](https://intranet.example.org/sites/hr/download.aspx?id=42)"), "The declared media type is enough when the address names no file type.");
    }

    [Test]
    public void AViewerLeadsToTheDocumentItShows()
    {
        var markdown = ExtractMarkdown("""<embed src="/viewer/web/viewer.html?file=%2Ffiles%2Fform.docx">""");
        Assert.That(markdown, Does.Contain("(https://intranet.example.org/files/form.docx)"), "The viewer page has no text of its own, so the link goes to the document it shows.");
    }

    [Test]
    public void AFrameWithoutADocumentIsRemovedAsBefore()
    {
        var markdown = ExtractMarkdown("""<iframe src="https://video.example.com/embed/123" title="Welcome video"></iframe><iframe src="/viewer.aspx?file=form.docx"></iframe>""");
        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Not.Contain("Embedded document"), "A video or a viewer whose parameter is only a file name holds nothing to read.");
            Assert.That(markdown, Does.Not.Contain("video.example.com"));
        });
    }

    [Test]
    public void AScriptAddressNeverBecomesALink()
    {
        var markdown = ExtractMarkdown("""<iframe src="javascript:alert('x.pdf')"></iframe>""");
        Assert.That(markdown, Does.Not.Contain("Embedded document"));
    }

    [Test]
    public void AHiddenFrameStaysHidden()
    {
        var markdown = ExtractMarkdown("""<div hidden><iframe src="/files/secret.pdf" title="Hidden"></iframe></div>""");
        Assert.That(markdown, Does.Not.Contain("secret.pdf"), "Whatever the page hides is removed with its frames, as before.");
    }
}