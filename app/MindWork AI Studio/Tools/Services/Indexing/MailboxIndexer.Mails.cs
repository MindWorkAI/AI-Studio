using System.Text;

using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Databases.VectorStore;
using AIStudio.Tools.Mail;
using AIStudio.Tools.Security;

using MailKit;

using MimeKit;

namespace AIStudio.Tools.Services.Indexing;

/// <remarks>
/// One mail at a time: what it is called in the index, how its text is read, and what the index
/// keeps of it beyond its chunks.
/// </remarks>
internal sealed partial class MailboxIndexer
{
    /// <summary>
    /// What the index stores as the type of a mail, where a file has its extension.
    /// </summary>
    private const string MAIL_FILE_TYPE = "mail";

    /// <summary>
    /// The content type of a header block on its own, cf. RFC 6522.
    /// </summary>
    private const string HEADER_BLOCK_CONTENT_TYPE = "text/rfc822-headers";

    /// <summary>
    /// How many fields of a mail pass the filter ahead of the names of its attachments: the header
    /// block, the body and the subject.
    /// </summary>
    private const int FILTERED_MAIL_FIELDS = 3;

    /// <summary>
    /// Links a mail the index holds already to its place in this folder, or reads and indexes it.
    /// </summary>
    private async Task SyncNewMailAsync(IndexedRunContext context, DataSourceMailbox mailbox, ImapMailboxConnector connector, string folderPath, IMessageSummary summary, DocumentRunProgress progress, ISet<string> encounteredKeys, CancellationToken token)
    {
        if (summary.Headers is not { } headers)
        {
            logger.LogWarning("The server delivered a mail of mailbox '{MailboxId}' without its header block. The mail is skipped.", mailbox.Id);
            return;
        }

        var key = MailContentKey.Create(MailSummaryReader.ReadIdentity(summary));
        var mailId = IndexedDocumentIds.CreateParentId(mailbox.Id, key);
        var location = new MailLocationRecord(folderPath, summary.UniqueId.Id, MailSummaryReader.ReadFlags(summary.Flags));
        encounteredKeys.Add(key);

        //
        // A mail the index holds already, which was moved or copied here, or numbered anew by the
        // server. It keeps its chunks and only gains a location.
        //
        if (await context.IndexStore.AddMailLocationAsync(mailbox.Id, mailId, location, token))
        {
            progress.RecordUnchanged();
            progress.Publish();
            return;
        }

        var mailHash = MailSummaryReader.ComputeMailHash(summary);
        if (context.Manifest.PermanentFailures.TryGetValue(key, out var permanentFailure) && string.Equals(permanentFailure.Fingerprint, mailHash, StringComparison.Ordinal))
        {
            progress.RecordStillUnreadable(key, permanentFailure);
            progress.Publish();
            return;
        }

        //
        // Named after the subject as the server delivered it. The name is for the user alone, who
        // reads it on the embeddings page as in any mail program. A model only ever gets to read
        // the filtered subject, which the index stores.
        //
        var subject = MailTextNormalization.NormalizeHeaderValue(headers[HeaderId.Subject]);
        var displayName = subject.Length > 0 ? subject : TB("(no subject)");

        var foundAtUtc = DateTimeOffset.UtcNow;
        var isNew = !context.Manifest.Files.ContainsKey(key);
        var document = this.CreateMailDocument(context, mailbox, summary, key, mailId, mailHash, displayName, foundAtUtc, null, []);

        try
        {
            var (text, attachments) = await this.ReadMailAsync(context, connector, mailbox, summary, token);
            document = this.CreateMailDocument(context, mailbox, summary, key, mailId, mailHash, displayName, foundAtUtc, text, attachments);

            var reportBlockProgress = progress.BeginDocument(document);
            var chunkCount = await context.IndexDocumentAsync(document, reportBlockProgress, token);
            await progress.RecordDocumentIndexedAsync(document, chunkCount, isNew, token);

            // Only after the last chunk: indexing the document deleted whatever the index kept of the mail.
            await context.IndexStore.UpsertMailAsync(mailbox.Id, CreateMailRecord(mailId, summary, text, attachments, mailHash, location, foundAtUtc), token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (MailboxConnectionException)
        {
            // Not about this one mail: the connection is gone, and every other mail would fail the same way.
            throw;
        }
        catch (VectorStoreUnreadableException)
        {
            // Not about this one mail either: the store of the whole mailbox cannot be opened.
            throw;
        }
        catch (Exception exception)
        {
            await progress.RecordDocumentFailureAsync(document, exception, token);
        }
    }

    /// <summary>
    /// Reads the text of a mail and of its attachments, filtered for prompt injections.
    /// </summary>
    /// <remarks>
    /// Of an encrypted mail, nothing but the header block is read: its text parts and its
    /// attachments stay on the server. The subject and the names of the attachments are filtered
    /// on their own as well, since the index stores them apart from the text, and whatever reads
    /// them there may hand them on to a model. A passage the filter removes from them is therefore
    /// reported twice.
    /// </remarks>
    private async Task<(MailText Text, IReadOnlyList<MailAttachmentText> Attachments)> ReadMailAsync(IndexedRunContext context, ImapMailboxConnector connector, DataSourceMailbox mailbox, IMessageSummary summary, CancellationToken token)
    {
        var textParts = MailEncryptionDetection.Detect(summary.Body) is MailEncryptionKind.NONE ? await connector.FetchTextPartsAsync(summary, token) : null;
        var text = MailTextBuilder.Build(MailSummaryReader.ReadTextSource(summary, textParts));

        // The text itself may reveal an encryption the structure did not, cf. MailTextBuilder:
        var attachmentParts = text.EncryptionKind is MailEncryptionKind.NONE ? MailSummaryReader.ReadAttachments(summary) : [];

        var source = PromptInjectionSource.MailContent(mailbox.Name);
        var filtered = await guardService.SanitizeAsync([
            new(text.HeaderBlock, source),
            new(text.Body, source),
            new(text.Subject, source),
            ..attachmentParts.Select(part => new PromptInjectionText(MailTextNormalization.NormalizeHeaderValue(part.FileName), source)),
        ]);

        var attachments = new List<MailAttachmentText>(attachmentParts.Count);
        for (var index = 0; index < attachmentParts.Count; index++)
            attachments.Add(await this.ReadAttachmentAsync(context, mailbox, connector, summary.UniqueId, attachmentParts[index], filtered[FILTERED_MAIL_FIELDS + index], token));

        return (text with { HeaderBlock = filtered[0], Body = filtered[1], Subject = filtered[2] }, attachments);
    }

    /// <summary>
    /// Describes a mail as a document for the shared part of an indexing run.
    /// </summary>
    /// <remarks>
    /// A mail has no path of its own. Where it lies is kept as its locations, which change without
    /// the mail being embedded again, so a folder stored here would soon name the wrong one.
    /// </remarks>
    /// <param name="context">The run the mail is indexed in.</param>
    /// <param name="mailbox">The mailbox the mail belongs to.</param>
    /// <param name="summary">The summary of the mail.</param>
    /// <param name="key">The content key of the mail.</param>
    /// <param name="mailId">The id of the mail, which is the id of its document.</param>
    /// <param name="mailHash">The hash of the mail, which is the fingerprint of its document.</param>
    /// <param name="displayName">How the mail is called in messages for the user.</param>
    /// <param name="foundAtUtc">When AI Studio found the mail.</param>
    /// <param name="text">The filtered text of the mail, or null while it is not read yet. Such a document has no chunks and only serves to record a failure.</param>
    /// <param name="attachments">The attachments of the mail, empty while it is not read yet.</param>
    /// <returns>The document.</returns>
    private EmbeddingDocument CreateMailDocument(IndexedRunContext context, DataSourceMailbox mailbox, IMessageSummary summary, string key, string mailId, string mailHash, string displayName, DateTimeOffset foundAtUtc, MailText? text, IReadOnlyList<MailAttachmentText> attachments)
    {
        var (sentAtUtc, receivedAtUtc) = ReadDates(summary, foundAtUtc);
        var state = new EmbeddingStateFile(
            mailId,
            key,
            text?.Subject ?? string.Empty,
            string.Empty,
            MAIL_FILE_TYPE,
            mailHash,
            summary.Size ?? 0,
            sentAtUtc ?? receivedAtUtc,
            receivedAtUtc,
            DateTimeOffset.UtcNow,
            0);

        //
        // The mail itself is one piece of text: the first chunk starts with the header block, so a
        // search for a sender or a subject finds the mail. The attachments follow it.
        //
        var fullText = text?.FullText ?? string.Empty;
        var content = new SegmentedText(fullText, fullText.Length is 0 ? [] : [new TextSegment(fullText, null, null)]);
        var chunkingOptions = DataSourceEmbeddingService.GetChunkingOptions(mailbox, context.EmbeddingProvider);

        return new(key, state, displayName, chunkToken => this.StreamMailChunksAsync(content, attachments, chunkingOptions, context.EmbeddingProvider, chunkToken));
    }

    /// <summary>
    /// Puts together what the index keeps about a mail beyond its chunks.
    /// </summary>
    /// <param name="mailId">The id of the mail, which is the id of its document.</param>
    /// <param name="summary">The summary of the mail, with its header block and its structure.</param>
    /// <param name="text">The filtered text of the mail.</param>
    /// <param name="attachments">The attachments of the mail. Each one is kept, with its text or with the reason why there is none.</param>
    /// <param name="mailHash">The hash of the mail, cf. MailSummaryReader.ComputeMailHash.</param>
    /// <param name="location">Where the mail was found.</param>
    /// <param name="foundAtUtc">When AI Studio found the mail. The index keeps the earliest time it found the mail.</param>
    /// <returns>The mail as the index keeps it.</returns>
    internal static MailRecord CreateMailRecord(string mailId, IMessageSummary summary, MailText text, IReadOnlyList<MailAttachmentText> attachments, string mailHash, MailLocationRecord location, DateTimeOffset foundAtUtc)
    {
        var headers = summary.Headers ?? throw new ArgumentException("The mail was fetched without its header block.", nameof(summary));
        var (sentAtUtc, receivedAtUtc) = ReadDates(summary, foundAtUtc);

        //
        // The header block as the server delivered it, encoded words and all, for whoever has to
        // judge the mail later. It is never cut into chunks, and it is filtered when it is read.
        //
        var headerBlock = MailSummaryReader.ReadHeaderBlock(headers);
        List<MailPartRecord> parts = [new(MailPartKind.HEADERS, string.Empty, HEADER_BLOCK_CONTENT_TYPE, Encoding.UTF8.GetByteCount(headerBlock), headerBlock, MailPartTextState.EXTRACTED)];

        var bodyPart = text.BodySource switch
        {
            MailBodySource.HTML => summary.HtmlBody,
            MailBodySource.PLAIN_TEXT => summary.TextBody,
            _ => null,
        };

        if (bodyPart is not null && text.Body.Length > 0)
            parts.Add(new(MailPartKind.BODY, string.Empty, bodyPart.ContentType.MimeType, bodyPart.Octets, text.Body, MailPartTextState.EXTRACTED));

        //
        // Every attachment, whether its text was read or not: that a mail has attachments, and
        // what they are called, is worth searching for either way.
        //
        parts.AddRange(attachments.Select(attachment => new MailPartRecord(
            MailPartKind.ATTACHMENT,
            attachment.Name,
            attachment.Part.ContentType.MimeType,
            attachment.Part.Octets,
            attachment.TextState is MailPartTextState.EXTRACTED ? attachment.Content.Text : null,
            attachment.TextState)));

        return new(
            mailId,
            MailHeaders.ReadMessageIds(headers, HeaderId.MessageId).FirstOrDefault(IsStorableMessageId) ?? string.Empty,
            MailHeaders.ReadMessageIds(headers, HeaderId.InReplyTo).FirstOrDefault(IsStorableMessageId) ?? string.Empty,
            MailHeaders.ReadMessageIds(headers, HeaderId.References).Where(IsStorableMessageId).ToList(),
            sentAtUtc,
            receivedAtUtc,
            text.Importance,
            text.EncryptionKind,
            mailHash,
            foundAtUtc,
            MailSummaryReader.ReadAddresses(headers),
            parts,
            [location]);
    }

    /// <summary>
    /// When the sender says the mail was written, and when it arrived at the server.
    /// </summary>
    /// <remarks>
    /// A server has to report when a mail arrived. Should one not do so, the date the sender gives
    /// comes closest, and after that the moment AI Studio found the mail.
    /// </remarks>
    private static (DateTimeOffset? SentAtUtc, DateTimeOffset ReceivedAtUtc) ReadDates(IMessageSummary summary, DateTimeOffset foundAtUtc)
    {
        var sentAtUtc = summary.Headers is { } headers ? MailHeaders.ReadDate(headers)?.ToUniversalTime() : null;
        return (sentAtUtc, summary.InternalDate?.ToUniversalTime() ?? sentAtUtc ?? foundAtUtc);
    }

    /// <summary>
    /// Whether the index can keep a Message-ID, which it stores separated by spaces, cf. UpsertMailAsync.
    /// </summary>
    private static bool IsStorableMessageId(string messageId) => messageId.Length > 0 && !messageId.Any(char.IsWhiteSpace);
}