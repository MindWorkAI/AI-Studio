namespace AIStudio.Chat;

/// <summary>
/// Everything a conversation would put into the next request, sorted by how it can be counted.
/// </summary>
/// <remarks>
/// Collected here rather than while counting, so that what counts towards a token budget is one
/// question with one answer which a test can ask. It follows what the message builder actually
/// sends: the system prompt, the text of every block, and the attachments hanging off those
/// blocks -- plus whatever is standing in the composer but has not been sent yet, because that is
/// the part a person is deciding about while they look at the number.
/// </remarks>
public sealed record ConversationParts
{
    /// <summary>
    /// A conversation with nothing in it.
    /// </summary>
    public static readonly ConversationParts NOTHING = new();

    /// <summary>
    /// The texts which go into the request as they are.
    /// </summary>
    public IReadOnlyList<string> Texts { get; init; } = [];

    /// <summary>
    /// The texts which are still being written.
    /// </summary>
    /// <remarks>
    /// They cost exactly what the others cost; what sets them apart is that they will never be seen
    /// again in this shape. The sentence somebody is typing changes with the next pause, and an
    /// answer being streamed is a different text three seconds later -- so remembering what they
    /// cost fills memory with answers nobody will ask for again.
    /// </remarks>
    public IReadOnlyList<string> GrowingTexts { get; init; } = [];

    /// <summary>
    /// The documents whose content is put into the request.
    /// </summary>
    public IReadOnlyList<FileAttachment> Documents { get; init; } = [];

    /// <summary>
    /// How many images travel along.
    /// </summary>
    public int Images { get; init; }

    /// <summary>
    /// Collects what a conversation would send.
    /// </summary>
    /// <remarks>
    /// Blocks without text are skipped, because the message builder skips them too: a block whose
    /// text is empty never becomes a message, whatever else hangs off it.
    /// </remarks>
    /// <param name="thread">The conversation so far, or null when there is none yet.</param>
    /// <param name="systemPrompt">
    /// The system prompt as it would be sent, which is not the one a person typed: a chat template
    /// may replace it, the retrieved data of a data source is appended to it, a profile adds a
    /// paragraph, and the tool policy adds another.
    /// </param>
    /// <param name="draft">What stands in the composer.</param>
    /// <param name="draftAttachments">What is attached to the composer.</param>
    /// <param name="imagesAreSent">Whether the model takes images at all. When it does not, none are sent.</param>
    /// <returns>The parts of the conversation.</returns>
    public static ConversationParts Of(ChatThread? thread, string systemPrompt, string draft, IEnumerable<FileAttachment>? draftAttachments, bool imagesAreSent)
    {
        var texts = new List<string>();
        var growing = new List<string>();
        var documents = new List<FileAttachment>();
        var images = 0;

        if (!string.IsNullOrWhiteSpace(systemPrompt))
            texts.Add(systemPrompt);

        if (thread is not null)
        {
            //
            // Blocks hidden from the user are counted like any other. They are hidden on the screen,
            // not in the request: the message builder sends them, so they take their tokens whether
            // or not anybody can see them.
            //
            foreach (var block in thread.Blocks)
            {
                if (block.ContentType is not ContentType.TEXT || block.Content is not ContentText text || string.IsNullOrWhiteSpace(text.Text))
                    continue;

                if (text.IsStreaming)
                    growing.Add(text.Text);
                else
                    texts.Add(text.Text);

                Sort(text.FileAttachments, documents, ref images);
            }
        }

        if (!string.IsNullOrWhiteSpace(draft))
            growing.Add(draft);

        if (draftAttachments is not null)
            Sort(draftAttachments, documents, ref images);

        return new()
        {
            Texts = texts,
            GrowingTexts = growing,
            Documents = documents,
            Images = imagesAreSent ? images : 0,
        };
    }

    /// <summary>
    /// Puts attachments into the two groups they are counted in.
    /// </summary>
    /// <remarks>
    /// An attachment whose file is gone is left out of both. It is not sent either: the message
    /// builder drops it and tells the person about it, so counting it would promise a request which
    /// is never made.
    /// </remarks>
    private static void Sort(IEnumerable<FileAttachment> attachments, List<FileAttachment> documents, ref int images)
    {
        foreach (var attachment in attachments)
        {
            if (!attachment.Exists)
                continue;

            switch (attachment.Type)
            {
                case FileAttachmentType.DOCUMENT:
                    documents.Add(attachment);
                    break;

                case FileAttachmentType.IMAGE:
                    images++;
                    break;
            }
        }
    }
}