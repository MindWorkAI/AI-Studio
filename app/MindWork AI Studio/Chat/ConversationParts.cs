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
    /// The texts which belong to this moment alone.
    /// </summary>
    /// <remarks>
    /// They cost exactly what the others cost; what sets them apart is that they will never be seen
    /// again in this shape. The sentence somebody is typing changes with the next pause, and an
    /// answer being streamed is a different text three seconds later -- so remembering what they
    /// cost fills memory with answers nobody will ask for again.
    ///
    /// What a model's tools have returned so far belongs here for the same reason, although nobody
    /// is writing it: it travels with every further round of one request and with nothing after
    /// that, so it is measured while it matters and forgotten when the answer is there.
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
    /// Which of the texts above are the message being written right now.
    /// </summary>
    /// <remarks>
    /// A marker, not a further part: everything named here also stands in <see cref="GrowingTexts"/>,
    /// and counting the parts counts each of them exactly once. It exists because the two halves of
    /// the number answer different questions. What the conversation has cost so far can be had
    /// exactly, from the provider which charged for it; what is about to be added to it can only be
    /// estimated. Told as one number, nobody can see which half they are looking at.
    /// </remarks>
    public IReadOnlyList<string> DraftTexts { get; init; } = [];

    /// <inheritdoc cref="DraftTexts"/>
    public IReadOnlyList<FileAttachment> DraftDocuments { get; init; } = [];

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
                growing.AddRange(text.PendingToolConversation);

                if (string.IsNullOrWhiteSpace(text.Text))
                    continue;

                if (text.IsStreaming)
                    growing.Add(text.Text);
                else
                    texts.Add(text.Text);

                Sort(text.FileAttachments, documents, ref images);
            }
        }

        var draftTexts = new List<string>();
        var draftDocuments = new List<FileAttachment>();

        if (!string.IsNullOrWhiteSpace(draft))
        {
            growing.Add(draft);
            draftTexts.Add(draft);
        }

        if (draftAttachments is not null)
        {
            var documentsBefore = documents.Count;
            Sort(draftAttachments, documents, ref images);

            //
            // Whatever sorting just appended is what the composer carries. Read off the list
            // rather than sorted a second time, so that a change to what counts as a document
            // cannot start meaning two different things in one method.
            //
            draftDocuments.AddRange(documents.Skip(documentsBefore));
        }

        return new()
        {
            Texts = texts,
            GrowingTexts = growing,
            Documents = documents,
            Images = imagesAreSent ? images : 0,
            DraftTexts = draftTexts,
            DraftDocuments = draftDocuments,
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