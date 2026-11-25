using AIStudio.Tools;
using AIStudio.Tools.Rust;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks when the user hears that a document opened without the page of its passage.
/// </summary>
/// <remarks>
/// Without the notice, the user takes the beginning of the document, or the place their program remembered,
/// for the passage. Each reason gets a notice of its own, since each tells why AI Studio could not do
/// otherwise. Nothing here asserts on the wording: I18N is process-wide state without a reset, so an Init
/// somewhere else would decide whether these pass. The page itself is inserted whatever the language.
/// </remarks>
[TestFixture]
public sealed class SourceDocumentLocationExtensionsTests
{
    private static readonly SourceDocumentLocation DOCUMENT_AT_PAGE_33 = new(Path.Combine(Path.GetTempPath(), "handbook.pdf"), 33);

    [TestCase(DocumentPageOutcome.SANDBOXED)]
    [TestCase(DocumentPageOutcome.PROGRAM_TAKES_NO_PAGE)]
    [TestCase(DocumentPageOutcome.PROGRAM_FAILED)]
    [TestCase(DocumentPageOutcome.UNKNOWN)]
    public void APageWhichDidNotArriveIsNamed(DocumentPageOutcome outcome)
    {
        var notice = DOCUMENT_AT_PAGE_33.GetPageNotice(new(true, outcome, string.Empty));
        Assert.That(notice, Does.Contain("33"));
    }

    [Test]
    public void EachReasonIsToldItsOwnWay()
    {
        var notices = new[] { DocumentPageOutcome.SANDBOXED, DocumentPageOutcome.PROGRAM_TAKES_NO_PAGE, DocumentPageOutcome.PROGRAM_FAILED }
            .Select(outcome => DOCUMENT_AT_PAGE_33.GetPageNotice(new(true, outcome, string.Empty)))
            .ToList();

        Assert.That(notices, Is.Unique);
    }

    [TestCase(DocumentPageOutcome.APPLIED)]
    [TestCase(DocumentPageOutcome.NOT_REQUESTED)]
    public void APageWhichArrivedOrWasNotAskedForNeedsNoNotice(DocumentPageOutcome outcome) => Assert.That(DOCUMENT_AT_PAGE_33.GetPageNotice(new(true, outcome, string.Empty)), Is.Null);

    [TestCase(null)]
    [TestCase(0)]
    public void ADocumentWithoutAPageNeedsNoNotice(int? pageNumber)
    {
        var document = DOCUMENT_AT_PAGE_33 with { PageNumber = pageNumber };
        Assert.That(document.GetPageNotice(new(true, DocumentPageOutcome.PROGRAM_TAKES_NO_PAGE, string.Empty)), Is.Null);
    }

    /// <summary>
    /// The user hears about a document which did not open as an error, and a notice about its page would only add to it.
    /// </summary>
    [TestCase(DocumentPageOutcome.DOCUMENT_NOT_OPENED)]
    [TestCase(DocumentPageOutcome.SANDBOXED)]
    public void ADocumentWhichDidNotOpenNeedsNoNotice(DocumentPageOutcome outcome) => Assert.That(DOCUMENT_AT_PAGE_33.GetPageNotice(new(false, outcome, "The path is not a file.")), Is.Null);
}