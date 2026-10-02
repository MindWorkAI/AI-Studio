using System.Text.Json;

using AIStudio.Provider;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Mail;
using AIStudio.Tools.Services;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks which mail and which part of it Read Mail reads, and how it pages through a long text.
/// </summary>
/// <remarks>
/// The model names a mail by an id it copied out of a search, and an attachment by its number. A
/// wrong one is refused with what would have been right, never read as something else. A long
/// text comes in pages, which together have to give the whole text: a character lost or repeated
/// between two pages is a sentence the model misquotes.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ReadMailToolTests : ToolRegistryTestBase
{
    private const string MAIL_ID = "5e2a9c1f-7b3d-4f8e-a6c4-1d9b0e7f3a52";

    [Test]
    public void AMailIdIsTakenInTheFormTheIndexStoresIt()
    {
        var request = ReadMailTool.ReadRequest(Arguments($$"""{"mail_id":"{{MAIL_ID.ToUpperInvariant()}}"}"""));

        Assert.Multiple(() =>
        {
            Assert.That(request.MailId, Is.EqualTo(MAIL_ID), "The index stores ids in lower case, and a model may write them in upper case.");
            Assert.That(request.AttachmentNumber, Is.Null, "Without an attachment, the text of the mail is read.");
            Assert.That(request.IncludeHeaders, Is.False);
            Assert.That(request.Page, Is.EqualTo(1));
        });
    }

    [TestCase("""{"mail_id":"the budget mail"}""")]
    [TestCase("""{"mail_id":"5e2a9c1f7b3d4f8ea6c41d9b0e7f3a52"}""")]
    [TestCase("""{"mail_id":"{5e2a9c1f-7b3d-4f8e-a6c4-1d9b0e7f3a52}"}""")]
    public void AnythingButTheIdOfAMailIsRefused(string json)
    {
        var message = Refusal(() => ReadMailTool.ReadRequest(Arguments(json)));

        Assert.That(message, Does.Contain("'mail_id' must be the mail_id of a mail exactly as search_mails shows it"));
    }

    [Test]
    public void AMissingMailIdIsRefused()
    {
        Assert.That(Refusal(() => ReadMailTool.ReadRequest(Arguments("""{}"""))), Does.Contain("Missing required argument 'mail_id'"));
    }

    [TestCase("""{"attachment":0}""")]
    [TestCase("""{"page":0}""")]
    public void NoNumberStartsBelowOne(string conditions)
    {
        var json = $$"""{"mail_id":"{{MAIL_ID}}",{{conditions[1..]}}""";

        Assert.That(Refusal(() => ReadMailTool.ReadRequest(Arguments(json))), Does.Contain("must be a positive integer"));
    }

    [Test]
    public void WithoutAnAttachmentTheTextOfTheMailIsRead()
    {
        var part = ReadMailTool.SelectPart(Mail(Body("The text."), Attachment("budget.xlsx")), attachmentNumber: null);

        Assert.That(part?.Kind, Is.EqualTo(MailPartKind.BODY));
    }

    [Test]
    public void AnAttachmentIsFoundByItsNumber()
    {
        var part = ReadMailTool.SelectPart(Mail(Body("The text."), Attachment("first.pdf"), Attachment("second.pdf")), attachmentNumber: 2);

        Assert.That(part?.Name, Is.EqualTo("second.pdf"), "The attachments are numbered from 1 in their order, without the text.");
    }

    [Test]
    public void ANumberBeyondTheAttachmentsIsRefusedWithHowManyThereAre()
    {
        var message = Refusal(() => ReadMailTool.SelectPart(Mail(Body("The text."), Attachment("first.pdf")), attachmentNumber: 2));

        Assert.That(message, Does.Contain("must be at most 1 for this mail, but was 2").And.Contain("Leave it out to read the text of the mail."));
    }

    [Test]
    public void AMailWithoutAttachmentsSaysSo()
    {
        var message = Refusal(() => ReadMailTool.SelectPart(Mail(Body("The text.")), attachmentNumber: 1));

        Assert.That(message, Does.Contain("it has no attachments"));
    }

    [TestCase("", 1)]
    [TestCase("0123456789", 1)]
    [TestCase("0123456789a", 2)]
    [TestCase("0123456789012345678901234", 3)]
    public void ATextFillsAsManyPagesAsItNeeds(string text, int expectedLastPage)
    {
        Assert.That(ReadMailTool.GetLastPage(text, 10), Is.EqualTo(expectedLastPage), "An empty text still has its one, empty page.");
    }

    [Test]
    public void ThePagesGiveTheWholeTextWithoutGapsOrRepeats()
    {
        var text = string.Concat(Enumerable.Range(0, 37).Select(index => (char)('a' + index % 26)));
        var lastPage = ReadMailTool.GetLastPage(text, 10);

        var pages = Enumerable.Range(1, lastPage).Select(page => ReadMailTool.GetPage(text, page, 10)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(string.Concat(pages), Is.EqualTo(text));
            Assert.That(pages.Take(lastPage - 1).Select(page => page.Length), Is.All.EqualTo(10));
        });
    }

    [Test]
    public void APageNeverPartsASurrogatePair()
    {
        // The emoji takes two chars, at positions 9 and 10, right across the end of the first page:
        var text = "012345678😀bcdefghij";

        var first = ReadMailTool.GetPage(text, 1, 10);
        var second = ReadMailTool.GetPage(text, 2, 10);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo("012345678"));
            Assert.That(second, Does.StartWith("😀"));
            Assert.That(first + second, Is.EqualTo(text));
        });
    }

    [TestCase(MailPartTextState.ATTACHMENTS_DISABLED)]
    [TestCase(MailPartTextState.TOO_LARGE)]
    [TestCase(MailPartTextState.UNSUPPORTED_TYPE)]
    [TestCase(MailPartTextState.EXTRACTION_FAILED)]
    public void AnUnreadAttachmentSaysWhy(MailPartTextState textState)
    {
        Assert.That(ReadMailTool.GetUnreadReason(textState), Is.Not.EqualTo(ReadMailTool.GetUnreadReason(MailPartTextState.UNKNOWN)), "Every known reason has a sentence of its own, so the model can tell the user what to change.");
    }

    [Test]
    public async Task TheRegistryTakesTheDefinition()
    {
        var registry = this.CreateRegistry(new TestTool(this.Tool().GetDefinition()));

        var runnableTools = await registry.GetRunnableToolsAsync(this.ContextFor(ToolCapableProvider()), [ToolSelectionRules.READ_MAIL_TOOL_ID], mayRunTools: true);

        Assert.That(runnableTools.Select(tool => tool.Definition.Id), Is.EqualTo(new[] { ToolSelectionRules.READ_MAIL_TOOL_ID }), "The registry drops a definition it cannot accept, with no more than a warning in the log.");
    }

    [Test]
    public void TheToolKeepsToTheRulesOfAMailbox()
    {
        var tool = this.Tool();

        Assert.Multiple(() =>
        {
            Assert.That(tool.IsAvailable, Is.False, "Without the previews, the tool does not exist.");
            Assert.That(tool.GetDefinition().MinimumProviderConfidence, Is.EqualTo(ConfidenceLevel.VERY_LOW), "Each mailbox asks for its own level, and none may ask for less.");
            Assert.That(tool.OutboundData, Is.EqualTo(ToolOutboundData.NONE), "Reading sends no query anywhere.");
            Assert.That(tool.ReturnsUntrustedExternalContent, Is.True, "Mails are written by others.");
        });
    }

    // Stating its definition and reading its arguments needs none of the services the tool reads with:
    private ReadMailTool Tool() => new(this.SettingsManager, new MailboxRetrievalService(this.SettingsManager, null!, null!, NullLogger<MailboxRetrievalService>.Instance), null!, NullLogger<ReadMailTool>.Instance);

    private static MailPartRecord Body(string text) => new(MailPartKind.BODY, string.Empty, "text/plain", text.Length, text, MailPartTextState.EXTRACTED);

    private static MailPartRecord Attachment(string name) => new(MailPartKind.ATTACHMENT, name, "application/pdf", 1_024, "The text of the attachment.", MailPartTextState.EXTRACTED);

    private static MailRecord Mail(params MailPartRecord[] parts) => new(
        MAIL_ID,
        "mail@example.org",
        string.Empty,
        [],
        null,
        new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero),
        MailImportance.NORMAL,
        MailEncryptionKind.NONE,
        "mail-hash",
        new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero),
        [new MailAddressRecord(MailAddressRole.FROM, "alice@example.org", "Alice")],
        parts,
        [new MailLocationRecord("INBOX", 1, new MailFlags(true, false, false))]);

    private static JsonElement Arguments(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    private static string Refusal(TestDelegate read) => Assert.Throws<ArgumentException>(read)!.Message;
}