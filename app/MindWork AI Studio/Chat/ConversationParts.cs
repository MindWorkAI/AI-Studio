using System.Text.Json;

using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Chat;

/// <summary>
/// Everything a conversation would put into the next request, sorted by how it can be counted.
/// </summary>
/// <remarks>
/// Collected here rather than while counting, so that what counts towards a token budget is one
/// question with one answer which a test can ask. It follows what the message builder actually
/// sends: the system prompt, the schema of every tool the model may call, the text of every block,
/// and the attachments hanging off those blocks -- plus whatever is standing in the composer but
/// has not been sent yet, because that is the part a person is deciding about while they look at
/// the number.
///
/// And, while a request is running, what its tools have returned so far. That is the one part
/// which is not about the next request but about the one in flight: it is what the model is
/// reading at this moment, it is what fills the window while somebody watches, and it is gone
/// again once the answer stands.
///
/// The three are kept apart, and nothing stands in two of them. The conversation so far is what a
/// provider may already have counted exactly; the draft is what nobody has counted yet; and the
/// tool conversation is what the number will lose again once the answer is there. Each of them is
/// counted once and named on its own, so that a person can tell which part they are looking at.
/// </remarks>
public sealed record ConversationParts
{
    /// <summary>
    /// A conversation with nothing in it.
    /// </summary>
    public static readonly ConversationParts NOTHING = new();

    /// <summary>
    /// The texts of the conversation which go into the request as they are.
    /// </summary>
    public IReadOnlyList<string> Texts { get; init; } = [];

    /// <summary>
    /// The texts of the conversation which are still being written.
    /// </summary>
    /// <remarks>
    /// They cost exactly what the others cost; what sets them apart is that they will never be seen
    /// again in this shape. An answer being streamed is a different text three seconds later -- so
    /// remembering what it cost fills memory with answers nobody will ask for again.
    /// </remarks>
    public IReadOnlyList<string> GrowingTexts { get; init; } = [];

    /// <summary>
    /// What the tools of the running request have returned so far, along with the calls to them.
    /// </summary>
    /// <remarks>
    /// Growing in the same way as the answer being streamed, and measured the same way. It travels
    /// with every further round of one request and with nothing after that, so it is measured while
    /// it matters and forgotten when the answer is there.
    /// </remarks>
    public IReadOnlyList<string> ToolConversation { get; init; } = [];

    /// <summary>
    /// The documents of the conversation whose content is put into the request.
    /// </summary>
    public IReadOnlyList<FileAttachment> Documents { get; init; } = [];

    /// <summary>
    /// How many images of the conversation travel along.
    /// </summary>
    public int Images { get; init; }

    /// <summary>
    /// What stands in the composer, or an empty string when nothing does.
    /// </summary>
    /// <remarks>
    /// Changes with the next pause, so it is measured like the texts which are still being written.
    /// </remarks>
    public string DraftText { get; init; } = string.Empty;

    /// <summary>
    /// The documents attached to the composer.
    /// </summary>
    public IReadOnlyList<FileAttachment> DraftDocuments { get; init; } = [];

    /// <summary>
    /// How many images attached to the composer travel along.
    /// </summary>
    /// <remarks>
    /// Apart from the images of the conversation, because only those can be part of what a
    /// provider has already counted.
    /// </remarks>
    public int DraftImages { get; init; }

    /// <summary>
    /// Collects what a conversation would send.
    /// </summary>
    /// <remarks>
    /// Blocks without text are skipped, because the message builder skips them too: a block whose
    /// text is empty never becomes a message, whatever else hangs off it. What such a block may
    /// still carry is the tool conversation of a request which is running right now -- that one
    /// does travel, and it is read before the text is looked at.
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
    /// <param name="toolDefinitions">
    /// The tools the model may call, filtered for the provider the same way they are before
    /// sending, or null when there are none.
    /// </param>
    /// <returns>The parts of the conversation.</returns>
    public static ConversationParts Of(ChatThread? thread, string systemPrompt, string draft, IEnumerable<FileAttachment>? draftAttachments, bool imagesAreSent, IEnumerable<ToolDefinition>? toolDefinitions)
    {
        var texts = new List<string>();
        var growing = new List<string>();
        var toolConversation = new List<string>();
        var documents = new List<FileAttachment>();
        var images = 0;

        if (!string.IsNullOrWhiteSpace(systemPrompt))
            texts.Add(systemPrompt);

        //
        // The tools ride along beside the messages, one schema each, in every single request of a
        // conversation. Counted with the lasting texts rather than with the growing ones: a schema
        // is the same string all session long, so measuring it once and remembering it is exactly
        // what the cache is for.
        //
        foreach (var definition in toolDefinitions ?? [])
            texts.Add(Describe(definition));

        if (thread is not null)
        {
            //
            // Blocks hidden from the user are counted like any other. They are hidden on the screen,
            // not in the request: the message builder sends them, so they take their tokens whether
            // or not anybody can see them.
            //
            foreach (var block in thread.Blocks)
            {
                if (block.ContentType is not ContentType.TEXT || block.Content is not ContentText text)
                    continue;

                //
                // Asked before the text is, because while a model calls tools there is no text yet:
                // the answer arrives in one piece at the end, and everything in between travels as
                // the tool conversation. A block skipped for having nothing to say is exactly the
                // block whose request is growing the fastest.
                //
                toolConversation.AddRange(text.PendingToolConversation);

                if (string.IsNullOrWhiteSpace(text.Text))
                    continue;

                if (text.IsStreaming)
                    growing.Add(text.Text);
                else
                    texts.Add(text.Text);

                Sort(text.FileAttachments, documents, ref images);
            }
        }

        //
        // Sorted the same way as the attachments of the conversation, into lists of their own.
        //
        var draftDocuments = new List<FileAttachment>();
        var draftImages = 0;
        if (draftAttachments is not null)
            Sort(draftAttachments, draftDocuments, ref draftImages);

        return new()
        {
            Texts = texts,
            GrowingTexts = growing,
            ToolConversation = toolConversation,
            Documents = documents,
            Images = imagesAreSent ? images : 0,
            DraftText = string.IsNullOrWhiteSpace(draft) ? string.Empty : draft,
            DraftDocuments = draftDocuments,
            DraftImages = imagesAreSent ? draftImages : 0,
        };
    }

    /// <summary>
    /// What one tool costs the request it is offered in.
    /// </summary>
    /// <remarks>
    /// Its name, what it tells the model it does, and the arguments it takes -- that is what the
    /// provider adapters put into the tool list of the request body. The wire shape differs
    /// between the APIs: they name the fields differently, and a strict schema is rewritten for
    /// the OpenAI ones. None of that changes the length by an amount which matters next to a
    /// conversation, and the number is reported as an estimate anyway.
    /// </remarks>
    /// <param name="definition">The tool as it was declared.</param>
    /// <returns>The text to count for it.</returns>
    private static string Describe(ToolDefinition definition)
    {
        var parameters = definition.Function.Parameters.ValueKind is JsonValueKind.Undefined
            ? string.Empty
            : definition.Function.Parameters.GetRawText();

        return $"{definition.Function.Name}{definition.Function.DescriptionForLLM}{parameters}";
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