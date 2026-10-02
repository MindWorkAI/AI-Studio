using System.Runtime.CompilerServices;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Mail;
using AIStudio.Tools.Security;

using MailKit;

namespace AIStudio.Tools.Services.Indexing;

/// <remarks>
/// The attachments of a mail: which of them are read, how, and how their text joins the chunks of
/// the mail.
/// </remarks>
internal sealed partial class MailboxIndexer
{
    /// <summary>
    /// Reads the text of one attachment, should the rules of the mailbox allow it.
    /// </summary>
    /// <remarks>
    /// An attachment which cannot be read costs the mail nothing but that attachment: the mail is
    /// indexed with the reason stored in place of the text. Only a lost connection fails the mail,
    /// since every other mail would fail the same way.
    /// </remarks>
    /// <param name="context">The run the mail is indexed in.</param>
    /// <param name="mailbox">The mailbox the mail belongs to.</param>
    /// <param name="connector">The connection, with the folder of the mail open.</param>
    /// <param name="uid">The UID of the mail.</param>
    /// <param name="part">The attachment, from the structure of the mail.</param>
    /// <param name="name">The file name of the attachment, filtered for prompt injections.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The attachment with its text, or with the reason why there is none.</returns>
    /// <exception cref="MailboxConnectionException">The server could not deliver the attachment.</exception>
    private async Task<MailAttachmentText> ReadAttachmentAsync(IndexedRunContext context, DataSourceMailbox mailbox, ImapMailboxConnector connector, UniqueId uid, BodyPartBasic part, string name, CancellationToken token)
    {
        if (MailAttachmentRules.GetReasonToSkip(part, MailAttachmentRules.GetMaxSizeMegabytes(mailbox)) is { } reasonToSkip)
            return MailAttachmentText.WithoutText(part, name, reasonToSkip);

        string? path = null;
        try
        {
            path = MailAttachmentFiles.CreatePath(MailAttachmentRules.GetExtension(part));
            await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await connector.FetchAttachmentAsync(uid, part, file, token);

            // Filtered by the runtime while it reads, and reported as content of the mailbox, not of a file the user never saw:
            var content = await ExtractedFileText.ReadAsync(rustService, path, context.EmbeddingProvider, PromptInjectionSource.MailContent(mailbox.Name), token);
            return new(part, name, MailPartTextState.EXTRACTED, content, TextChunker.GetStrategyForFile(path));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (MailboxConnectionException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "The text of an attachment of a mail in mailbox '{MailboxId}' could not be read. The mail is indexed without it.", mailbox.Id);
            return MailAttachmentText.WithoutText(part, name, MailPartTextState.EXTRACTION_FAILED);
        }
        finally
        {
            if (path is not null)
                MailAttachmentFiles.Delete(path, logger);
        }
    }

    /// <summary>
    /// Cuts a mail into chunks: first its own text, then the text of each attachment.
    /// </summary>
    /// <remarks>
    /// Each attachment is cut on its own, by the strategy of its file type, and its first chunk
    /// names it. A chunk of an attachment carries no page: the chunks of a mail are told apart by
    /// the mail alone, so a page would not say which attachment it is a page of.
    /// </remarks>
    /// <param name="mailContent">The header block and the text of the mail.</param>
    /// <param name="attachments">The attachments, those whose text was not read included.</param>
    /// <param name="options">How large a chunk may become.</param>
    /// <param name="embeddingProvider">The embedding provider whose tokenizer measures the chunks.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The chunks of the mail, in order.</returns>
    private async IAsyncEnumerable<EmbeddingChunk> StreamMailChunksAsync(SegmentedText mailContent, IReadOnlyList<MailAttachmentText> attachments, ChunkingOptions options, EmbeddingProvider embeddingProvider, [EnumeratorCancellation] CancellationToken token)
    {
        await foreach (var chunk in textChunker.SplitAsync(mailContent, TextChunker.DOCUMENT_STRATEGY, options, embeddingProvider, token))
            yield return chunk;

        foreach (var attachment in attachments)
        {
            if (attachment.TextState is not MailPartTextState.EXTRACTED || attachment.Content.Text.Length is 0)
                continue;

            // In English like the header block, cf. MailTextBuilder:
            var heading = attachment.Name.Length > 0 ? $"Attachment: {attachment.Name}" : "Attachment without a name";
            await foreach (var chunk in textChunker.SplitAsync(attachment.Content, attachment.Strategy, options, embeddingProvider, token, heading))
                yield return chunk with { PageNumber = null };
        }
    }
}