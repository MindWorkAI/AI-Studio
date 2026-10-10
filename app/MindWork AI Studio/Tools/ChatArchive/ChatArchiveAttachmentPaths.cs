using AIStudio.Chat;

namespace AIStudio.Tools.ChatArchive;

/// <summary>
/// Visits every file path stored inside a chat thread. Export and import use this to
/// translate between absolute paths on this machine and archive-relative paths.
/// </summary>
public static class ChatArchiveAttachmentPaths
{
    /// <summary>
    /// Replaces every file path of the given chat thread by the result of the rewrite function.
    /// Paths which the rewrite function returns unchanged stay untouched.
    /// </summary>
    /// <param name="chat">The chat thread to process. It is modified in place.</param>
    /// <param name="rewrite">Maps an existing path to its replacement.</param>
    public static void Rewrite(ChatThread chat, Func<string, string> rewrite)
    {
        foreach (var block in chat.Blocks)
        {
            if (block.Content is not { } content)
                continue;

            RewriteAttachments(content.FileAttachments, rewrite);

            // Images might reference a file on this machine as well:
            if (content is ContentImage { SourceType: ContentImageSource.LOCAL_PATH } image && !string.IsNullOrWhiteSpace(image.Source))
                image.Source = rewrite(image.Source);
        }

        // Transcripts which were prepared for the composer but not sent yet:
        for (var index = 0; index < chat.PendingMediaTranscripts.Count; index++)
        {
            var transcript = chat.PendingMediaTranscripts[index];
            if (string.IsNullOrWhiteSpace(transcript.FilePath))
                continue;

            chat.PendingMediaTranscripts[index] = transcript with { FilePath = rewrite(transcript.FilePath) };
        }
    }

    private static void RewriteAttachments(List<FileAttachment> attachments, Func<string, string> rewrite)
    {
        for (var index = 0; index < attachments.Count; index++)
        {
            var attachment = attachments[index];
            if (string.IsNullOrWhiteSpace(attachment.FilePath))
                continue;

            var rewrittenPath = rewrite(attachment.FilePath);
            if (rewrittenPath == attachment.FilePath)
                continue;

            attachments[index] = attachment switch
            {
                ManagedTranscriptAttachment managed => managed with { FilePath = rewrittenPath },
                FileAttachmentImage image => image with { FilePath = rewrittenPath },

                _ => attachment with { FilePath = rewrittenPath },
            };
        }
    }
}