using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Mail;
using AIStudio.Tools.Services;
using AIStudio.Tools.Services.Indexing;

using MailKit;

using Microsoft.Extensions.Logging.Abstractions;

using MimeKit;

namespace AIStudio.Tests.Tools.Mail;

/// <summary>
/// Checks what the index keeps of a mail beyond its chunks.
/// </summary>
/// <remarks>
/// The header block is kept as the server sent it, since a later trust check has to see its encoded
/// words. The text is kept as the prompt injection filter left it, since that is what tools hand to
/// a model.
/// </remarks>
[TestFixture]
public sealed class MailboxIndexerTests
{
    private const string MAIL_ID = "2b0f9c8e-5d41-4c6e-9a7b-3e1f0d2c4b6a";

    /// <summary>
    /// Where a part sits in the mail, e.g. "1.2". Nothing here looks at it.
    /// </summary>
    private const string PART_SPECIFIER = "1";

    private static readonly MailLocationRecord LOCATION = new("INBOX", 42, new MailFlags(IsSeen: false, IsFlagged: true, IsAnswered: false));

    private static readonly DateTimeOffset FOUND_AT = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);

    [Test]
    public void TheHeaderBlockStaysRawAndTheBodyFiltered()
    {
        var headers = MailFixtures.Load("priority-with-attachment.eml").Headers;
        var summary = new MessageSummary(0)
        {
            Headers = headers,
            Body = new BodyPartText(new ContentType("text", "plain"), PART_SPECIFIER) { Octets = 61 },
        };

        var text = MailTextBuilder.Build(MailSummaryReader.ReadTextSource(summary, new MailTextParts(null, "Please review the attached report before the board meeting.")));
        var record = MailboxIndexer.CreateMailRecord(MAIL_ID, summary, text with { Body = "Please review the attached report." }, [], "HASH", LOCATION, FOUND_AT);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.Parts.Select(part => part.Kind), Is.EqualTo(new[] { MailPartKind.HEADERS, MailPartKind.BODY }));
            Assert.That(record.Parts[0].Text, Is.EqualTo(MailSummaryReader.ReadHeaderBlock(headers)));
            Assert.That(record.Parts[1], Is.EqualTo(new MailPartRecord(MailPartKind.BODY, string.Empty, "text/plain", 61, "Please review the attached report.", MailPartTextState.EXTRACTED)), "The body is not the filtered one, or it names another part than the one it was read from.");
            Assert.That(record.MessageId, Is.EqualTo("priority-with-attachment@example.org"));
            Assert.That(record.Importance, Is.EqualTo(MailImportance.HIGH));
            Assert.That(record.SentAtUtc, Is.EqualTo(new DateTimeOffset(2026, 9, 30, 14, 10, 0, TimeSpan.Zero)));
            Assert.That(record.ReceivedAtUtc, Is.EqualTo(record.SentAtUtc), "Without the arrival time of the server, the date of the sender stands in.");
            Assert.That(record.FirstSeenUtc, Is.EqualTo(FOUND_AT));
            Assert.That(record.Locations, Is.EqualTo(new[] { LOCATION }));
        }
    }

    [Test]
    public void AnEncryptedMailKeepsItsHeaderBlockAlone()
    {
        var summary = new MessageSummary(0)
        {
            Headers = MailFixtures.Load("smime-enveloped.eml").Headers,
            Body = new BodyPartBasic(new ContentType("application", "pkcs7-mime") { Parameters = { { "smime-type", "enveloped-data" } } }, PART_SPECIFIER),
            InternalDate = new DateTimeOffset(2026, 9, 30, 15, 0, 5, TimeSpan.FromHours(2)),
        };

        var text = MailTextBuilder.Build(MailSummaryReader.ReadTextSource(summary, null));
        var record = MailboxIndexer.CreateMailRecord(MAIL_ID, summary, text, [], "HASH", LOCATION, FOUND_AT);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.Parts.Select(part => part.Kind), Is.EqualTo(new[] { MailPartKind.HEADERS }), "Something of the encrypted content is kept.");
            Assert.That(record.EncryptionKind, Is.EqualTo(MailEncryptionKind.SMIME));
            Assert.That(record.ReceivedAtUtc, Is.EqualTo(new DateTimeOffset(2026, 9, 30, 13, 0, 5, TimeSpan.Zero)));
            Assert.That(record.ReceivedAtUtc.Offset, Is.EqualTo(TimeSpan.Zero));
        }
    }

    [Test]
    public void EveryAttachmentIsKeptWithItsTextOrWhyThereIsNone()
    {
        var summary = new MessageSummary(0)
        {
            Headers = MailFixtures.Load("priority-with-attachment.eml").Headers,
            Body = new BodyPartText(new ContentType("text", "plain"), PART_SPECIFIER) { Octets = 61 },
        };

        var report = Attachment("application", "pdf", 4096);
        var video = Attachment("video", "mp4", 80_000_000);
        var text = MailTextBuilder.Build(MailSummaryReader.ReadTextSource(summary, new MailTextParts(null, "Please review the attached report.")));
        var record = MailboxIndexer.CreateMailRecord(MAIL_ID, summary, text, [
            new MailAttachmentText(report, "board-report.pdf", MailPartTextState.EXTRACTED, new SegmentedText("Revenue rose by four percent.", []), TextChunker.DOCUMENT_STRATEGY),
            MailAttachmentText.WithoutText(video, "keynote.mp4", MailPartTextState.UNSUPPORTED_TYPE),
        ], "HASH", LOCATION, FOUND_AT);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.Parts.Select(part => part.Kind), Is.EqualTo(new[] { MailPartKind.HEADERS, MailPartKind.BODY, MailPartKind.ATTACHMENT, MailPartKind.ATTACHMENT }));
            Assert.That(record.Parts[2], Is.EqualTo(new MailPartRecord(MailPartKind.ATTACHMENT, "board-report.pdf", "application/pdf", 4096, "Revenue rose by four percent.", MailPartTextState.EXTRACTED)));
            Assert.That(record.Parts[3], Is.EqualTo(new MailPartRecord(MailPartKind.ATTACHMENT, "keynote.mp4", "video/mp4", 80_000_000, null, MailPartTextState.UNSUPPORTED_TYPE)), "An attachment without text is still an attachment, and a search for mails with attachments has to find it.");
        }
    }

    /// <remarks>
    /// A chunk of an attachment names the attachment by the position the index gives it among all
    /// attachments of the mail, those without text included, and keeps its pages. So a passage can
    /// say "page 2 of board-report.pdf", even when two attachments have the same name.
    /// </remarks>
    [Test]
    public async Task EveryChunkOfAnAttachmentKnowsItsAttachmentAndItsPages()
    {
        using var tokenizer = new WordCountingTokenizer();
        using var rustService = new RustService(tokenizer);
        var chunker = new TextChunker(rustService, NullLogger.Instance);
        var embeddingProvider = new EmbeddingProvider(1, "embedding-provider", "Embeddings", LLMProviders.SELF_HOSTED, new Model("embeddings", "embeddings"));

        const string MAIL_TEXT = "Please review the attached report.";
        var mailText = new SegmentedText(MAIL_TEXT, [new TextSegment(MAIL_TEXT, null, default)]);
        var report = new SegmentedText("Revenue rose.\nCosts fell.", [new TextSegment("Revenue rose.", 2, SourceLocation.OnPage(1)), new TextSegment("Costs fell.", 2, SourceLocation.OnPage(2))]);
        IReadOnlyList<MailAttachmentText> attachments =
        [
            MailAttachmentText.WithoutText(Attachment("video", "mp4", 80_000_000), "keynote.mp4", MailPartTextState.UNSUPPORTED_TYPE),
            new MailAttachmentText(Attachment("application", "pdf", 4096), "board-report.pdf", MailPartTextState.EXTRACTED, report, TextChunker.DOCUMENT_STRATEGY),
        ];

        var chunks = new List<EmbeddingChunk>();
        await foreach (var chunk in MailboxIndexer.StreamMailChunksAsync(chunker, mailText, attachments, new ChunkingOptions(100, 0), embeddingProvider, CancellationToken.None))
            chunks.Add(chunk);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chunks.Select(chunk => chunk.AttachmentPosition), Is.EqualTo(new int?[] { null, 1 }), "The text of the mail is no attachment, and the report is the second attachment, after the video without text.");
            Assert.That(chunks[1].Text, Does.StartWith("Attachment: board-report.pdf"));
            Assert.That((chunks[1].PageNumber, chunks[1].EndPageNumber), Is.EqualTo(((int?)1, (int?)2)), "The chunk keeps the pages of the report it spans.");
        }
    }

    private static BodyPartBasic Attachment(string mediaType, string mediaSubtype, uint octets) => new(new ContentType(mediaType, mediaSubtype), PART_SPECIFIER)
    {
        ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
        Octets = octets,
    };
}