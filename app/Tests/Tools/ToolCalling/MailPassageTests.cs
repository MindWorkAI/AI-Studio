using AIStudio.Tools;
using AIStudio.Tools.Databases.ProvenanceStore;
using AIStudio.Tools.RAG;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks the passage a mail gets, which lets the user read under its source what the AI received of it.
/// </summary>
/// <remarks>
/// A mail is listed without a link, so this passage is the only way to see what the AI got: the
/// header it saw, and the text or the passage found, where it got one. Nothing here asserts on the
/// translated labels.
/// </remarks>
[TestFixture]
public sealed class MailPassageTests
{
    private static readonly Source MAIL_SOURCE = new("Mail: Budget 2026 — Anna Muster, 2026-10-20", SourceExtensions.CreateMailSourceUrl("6a1f2b3c-4d5e-4f60-8a7b-9c0d1e2f3a4b", "mail-1"), SourceOrigin.TOOL);

    [Test]
    public void TheMailShowsItsHeaderAndThenItsText()
    {
        var content = MailToolResults.ComposePassage("Anna Muster <anna@example.org>", ["Ben <ben@example.org>", "Cleo <cleo@example.org>"], "Budget 2026", "2026-10-20T14:03+02:00", ["report.pdf"], null, "The budget grows by 4 %.");

        Assert.Multiple(() =>
        {
            Assert.That(content, Does.Contain("Anna Muster <anna@example.org>"));
            Assert.That(content, Does.Contain("Ben <ben@example.org>, Cleo <cleo@example.org>"));
            Assert.That(content, Does.Contain("Budget 2026"));
            Assert.That(content, Does.Contain("2026-10-20T14:03+02:00"), "The time as the model got it.");
            Assert.That(content, Does.Contain("report.pdf"));
            Assert.That(content, Does.EndWith("The budget grows by 4 %."), "The text follows the header.");
        });
    }

    [Test]
    public void AMailListedWithoutTextShowsItsHeaderAlone()
    {
        var content = MailToolResults.ComposePassage("Anna Muster <anna@example.org>", [], "Budget 2026", "2026-10-20T14:03+02:00", [], null, null);

        Assert.Multiple(() =>
        {
            Assert.That(content, Does.Contain("Budget 2026"), "A mail listed by sender or date is still something the model received.");
            Assert.That(content, Does.Not.Contain("---"), "There is no text to set apart.");
            Assert.That(content.Split('\n'), Has.Length.EqualTo(3), "Empty fields are left out: sender, subject, and time remain.");
        });
    }

    [Test]
    public void APassageFromAnAttachmentNamesTheAttachment()
    {
        var content = MailToolResults.ComposePassage("Anna Muster <anna@example.org>", [], "Budget 2026", "2026-10-20T14:03+02:00", ["report.pdf"], "report.pdf, page 3", "Revenue rose.");

        Assert.That(content, Does.Contain("report.pdf, page 3"));
    }

    [Test]
    public void TheMailPassageIsListedUnderItsSourceWithoutAPath()
    {
        var passage = MAIL_SOURCE.ToMailPassage("The budget grows by 4 %.");

        Assert.Multiple(() =>
        {
            Assert.That(new[] { passage }.GetPassagesOf(MAIL_SOURCE), Is.EqualTo(new[] { passage }), "Only a passage under its source gets the eye next to it.");
            Assert.That(passage.Path, Is.Empty, "The address of a mail names it for AI Studio only, so there is nothing to copy or open.");
            Assert.That(passage.DataSourceName, Is.EqualTo(MAIL_SOURCE.Title));
            Assert.That(passage.GetProvenanceKind(), Is.EqualTo(ProvenanceKind.MAIL));
        });
    }
}