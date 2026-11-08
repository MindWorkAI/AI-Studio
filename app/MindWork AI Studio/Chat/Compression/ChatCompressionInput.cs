using System.Globalization;
using System.Text;

namespace AIStudio.Chat.Compression;

/// <summary>
/// Turns the messages to summarize into the text the AI reads, and splits that text into parts which fit the model.
/// </summary>
/// <remarks>
/// Each message reads as the AI received it in the chat, with the content of its documents and
/// transcripts. Attached images are only named: the summary knows of them what the conversation
/// said about them. Only the last exchange reads as its bare text, since it follows the summary
/// word for word, its files included.<br/><br/>
///
/// A chat is compressed when it has grown large, often because it no longer fits the model, so a
/// single request with all of it would fail just the same. The text is therefore split into parts,
/// and the AI summarizes them one after the other, each time on top of its summary so far. A
/// message larger than a whole part, e.g., one with a long document, is split across several
/// parts, so that nothing of it gets lost.
/// </remarks>
public static class ChatCompressionInput
{
    /// <summary>
    /// Begins a part which carries on with a message the part before ended with.
    /// </summary>
    internal const string CONTINUATION_MARKER = "[... continuation of the message the previous part ended with ...]\n";

    /// <summary>
    /// Ends a part whose last message continues in the next part.
    /// </summary>
    internal const string TO_BE_CONTINUED_MARKER = "\n[... the message continues in the next part ...]";

    /// <summary>
    /// How much of a text fits, measured against what the estimate says would fit exactly.
    /// </summary>
    /// <remarks>
    /// The tokens of a piece of a text are only estimated from its characters, and a few tokens go to
    /// the markers, so splitting leaves some room.
    /// </remarks>
    private const double CUT_MARGIN = 0.9;

    /// <summary>
    /// Renders one message for the AI which writes the summary.
    /// </summary>
    /// <remarks>
    /// A heading names the number, the author, and the time, so that the summary can keep the order
    /// and tell who said what. Messages hidden from the user, such as the prompts an assistant sent
    /// into the chat, are marked: the user never saw them, which matters for how the summary words
    /// them. So are the messages of the last exchange, which the summary must neither summarize nor
    /// repeat. The time is the stored one, in invariant notation, so that a summary does not depend
    /// on the language of the app.
    /// </remarks>
    /// <param name="block">The block of the message. It must be one which the message builder sends, see ChatCompressionPlan.</param>
    /// <param name="number">The number of the message in the conversation, counted from 1.</param>
    /// <param name="text">The text of the message: the one the AI received in the chat, with the content of its documents, or the bare text of a message of the last exchange.</param>
    /// <param name="kept">Whether the message follows the summary word for word.</param>
    /// <returns>The message as the AI reads it.</returns>
    public static string Render(ContentBlock block, int number, string text, KeptMessage kept)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"[Message {number}, {RoleName(block.Role)}, {block.Time.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}");
        if (block.HideFromUser)
            sb.Append(", not shown to the user");

        sb.Append(kept switch
        {
            KeptMessage.LAST_QUESTION => ", the last message of the user, which follows your summary word for word together with its files",
            KeptMessage.LAST_ANSWER => ", the last answer, which follows your summary word for word",

            _ => string.Empty,
        });

        sb.Append("]\n");
        sb.Append(text.Trim());
        sb.Append('\n');

        var attachments = (block.Content as ContentText)?.FileAttachments ?? [];
        if (attachments.Count > 0)
            sb.Append("Attached files: ").Append(string.Join(", ", attachments.Select(Describe))).Append('\n');

        return sb.ToString();
    }

    /// <summary>
    /// Splits the messages into parts of at most the given number of tokens each.
    /// </summary>
    /// <remarks>
    /// The rules are those of TakePart, with the same room for every part.
    /// </remarks>
    /// <param name="messages">The rendered messages with their tokens, in the order of the conversation.</param>
    /// <param name="tokensPerPart">How many tokens of messages one part may hold.</param>
    /// <returns>The parts, each one the text of its messages. Empty without messages.</returns>
    public static IReadOnlyList<string> SplitIntoParts(IReadOnlyList<CountedMessage> messages, int tokensPerPart)
    {
        var parts = new List<string>();
        var (message, offset) = (0, 0);
        while (message < messages.Count)
        {
            var (part, nextMessage, nextOffset) = TakePart(messages, message, offset, tokensPerPart);
            parts.Add(part);
            (message, offset) = (nextMessage, nextOffset);
        }

        return parts;
    }

    /// <summary>
    /// Takes the next part of the messages, as many as fit into the given number of tokens.
    /// </summary>
    /// <remarks>
    /// The summary asks for one part at a time, because the room for a part shrinks as the summary
    /// so far grows, which only shows after each answer.<br/><br/>
    ///
    /// Messages stay whole and in their order where they can. A message which does not fit into
    /// what is left of the part starts the next one. A message larger than a whole part is split:
    /// the part takes as much of it as fits, preferably up to the end of a line, and the next part
    /// carries on where it ended. Markers tell the AI where a message breaks off and goes on. A split
    /// never falls between the two halves of a surrogate pair.
    /// </remarks>
    /// <param name="messages">The rendered messages with their tokens, in the order of the conversation.</param>
    /// <param name="startMessage">The index of the first message of the part.</param>
    /// <param name="startOffset">Where in the text of that message the part begins: 0, unless the part before split it.</param>
    /// <param name="tokensPerPart">How many tokens of messages the part may hold.</param>
    /// <returns>The text of the part, and where the next part begins: the index of its first message, and the offset in the text of that message.</returns>
    internal static (string Text, int NextMessage, int NextOffset) TakePart(IReadOnlyList<CountedMessage> messages, int startMessage, int startOffset, int tokensPerPart)
    {
        var part = new StringBuilder();
        var partTokens = 0;
        var index = startMessage;
        var offset = startOffset;
        while (index < messages.Count)
        {
            var message = messages[index];
            if (offset >= message.Text.Length)
            {
                (index, offset) = (index + 1, 0);
                continue;
            }

            var rest = message.Text.Length - offset;
            var tokens = offset is 0 ? message.Tokens : TokensOfPiece(message, rest);
            var marker = offset is 0 ? string.Empty : CONTINUATION_MARKER;
            if (tokens <= tokensPerPart - partTokens)
            {
                if (part.Length > 0)
                    part.Append('\n');

                part.Append(marker).Append(message.Text, offset, rest);
                partTokens += tokens;
                (index, offset) = (index + 1, 0);
                continue;
            }

            // A message which does not fit into what is left starts the next part:
            if (part.Length > 0)
                break;

            // A message larger than a whole part is split, and the next part carries on with it:
            var length = PieceLength(message.Text, offset, rest, tokens, tokensPerPart);
            part.Append(marker).Append(message.Text, offset, length).Append(TO_BE_CONTINUED_MARKER);
            return (part.ToString(), index, offset + length);
        }

        return (part.ToString(), index, offset);
    }

    /// <summary>
    /// Estimates the tokens of the end of a message, from the characters per token of the whole message.
    /// </summary>
    private static int TokensOfPiece(CountedMessage message, int length) => (int)Math.Ceiling(message.Tokens * (double)length / message.Text.Length);

    /// <summary>
    /// How many characters of a message, from the given offset on, fit into a part.
    /// </summary>
    /// <remarks>
    /// At least one character, so that every part moves on, and less than the rest, which does not
    /// fit. A line break in the last tenth of the piece ends it, so that lines stay whole.
    /// </remarks>
    private static int PieceLength(string text, int offset, int rest, int restTokens, int tokensPerPart)
    {
        var length = (int)(rest * (Math.Max(0, tokensPerPart) / (double)restTokens) * CUT_MARGIN);
        length = Math.Clamp(length, 1, Math.Max(1, rest - 1));

        var searchLength = Math.Max(1, length / 10);
        var lineBreak = text.LastIndexOf('\n', offset + length - 1, searchLength);
        if (lineBreak >= offset)
            length = lineBreak - offset + 1;

        if (char.IsHighSurrogate(text[offset + length - 1]))
            length = length > 1 ? length - 1 : Math.Min(rest, length + 1);

        return length;
    }

    private static string RoleName(ChatRole role) => role switch
    {
        ChatRole.USER => "user",
        ChatRole.AI => "AI",
        ChatRole.AGENT => "AI agent",
        ChatRole.SYSTEM => "system",

        _ => "unknown",
    };

    private static string Describe(FileAttachment attachment) => attachment switch
    {
        ManagedTranscriptAttachment transcript => $"{transcript.OriginalFileName} (transcript)",
        { Type: FileAttachmentType.IMAGE } => $"{attachment.FileName} (image)",
        { Type: FileAttachmentType.AUDIO } => $"{attachment.FileName} (audio)",

        _ => attachment.FileName,
    };
}