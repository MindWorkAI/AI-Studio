using System.Text.Json;

using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks that the app reads what the runtime tells about the page of a document it opened.
/// </summary>
/// <remarks>
/// PageOutcome in runtime/src/file_actions.rs names its values in Pascal case, and the app tells the user from
/// them why a document opened without its page. A name which the app spelled differently would leave the user
/// without the reason, and failing nowhere. The test the_app_reads_the_page_outcome_by_these_names checks the
/// same names on the side of the runtime. The answers are read with the options of RustService itself.
/// </remarks>
[TestFixture]
public sealed class OpenDocumentResponseTests
{
    [TestCase("Applied", DocumentPageOutcome.APPLIED)]
    [TestCase("NotRequested", DocumentPageOutcome.NOT_REQUESTED)]
    [TestCase("Sandboxed", DocumentPageOutcome.SANDBOXED)]
    [TestCase("ProgramTakesNoPage", DocumentPageOutcome.PROGRAM_TAKES_NO_PAGE)]
    [TestCase("ProgramFailed", DocumentPageOutcome.PROGRAM_FAILED)]
    [TestCase("DocumentNotOpened", DocumentPageOutcome.DOCUMENT_NOT_OPENED)]
    public void TheAppReadsWhatBecameOfThePage(string runtimeName, DocumentPageOutcome expected)
    {
        var response = JsonSerializer.Deserialize<OpenDocumentResponse>($$"""{"success":true,"page_outcome":"{{runtimeName}}","issue":""}""", RustService.JSON_OPTIONS);

        Assert.That(response, Is.EqualTo(new OpenDocumentResponse(true, expected, string.Empty)));
    }

    [Test]
    public void AnOutcomeOfANewerRuntimeIsUnknown()
    {
        var response = JsonSerializer.Deserialize<OpenDocumentResponse>("""{"success":true,"page_outcome":"ViewerOnTheMoon","issue":""}""", RustService.JSON_OPTIONS);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.Success, Is.True);
            Assert.That(response.PageOutcome, Is.EqualTo(DocumentPageOutcome.UNKNOWN));
        }
    }
}