using System.Globalization;
using System.Text;

namespace AIStudio.Chat.Compression;

/// <summary>
/// Turns the messages to summarize into the text the AI reads, and splits that text into parts which fit the model.
/// </summary>
/// <remarks>
/// The summary is asked for when a chat has grown large, often because it no longer fits the model.
/// A request which handed the AI the whole conversation as the chat sends it would then fail just
/// the same. So the AI gets the texts of the messages only: attached documents and images stand in
/// by their names, since what the conversation learned from them is in the answers anyway. Whatever
/// is still too long for one request is split into parts, and the AI summarizes them one after the
/// other, each time on top of its summary so far.
/// </remarks>
public static class ChatCompressionInput
{
    /// <summary>
    /// Stands for the middle of a message which is too long for a part of its own.
    /// </summary>
    private const string LEFT_OUT_MARKER = "[... {0} characters left out ...]";

    /// <summary>
    /// How much of a message cut in the middle stays, measured against what would fit exactly.
    /// </summary>
    /// <remarks>
    /// The tokens of the cut message are only estimated from its characters, and a few tokens go to
    /// the marker, so the cut leaves some room.
    /// </remarks>
    private const double CUT_MARGIN = 0.9;

    /// <summary>
    /// Renders one message for the AI which writes the summary.
    /// </summary>
    /// <remarks>
    /// A heading names the number, the author, and the time, so that the summary can keep the order
    /// and tell who said what. Messages hidden from the user, such as the prompts an assistant sent
    /// into the chat, are marked: the user never saw them, which matters for how the summary words
    /// them. The time is the stored one, in invariant notation, so that a summary does not depend on
    /// the language of the app.
    /// </remarks>
    /// <param name="block">The block of the message. It must be one which the message builder sends, see ChatCompressionPlan.</param>
    /// <param name="number">The number of the message in the conversation, counted from 1.</param>
    /// <param name="isLastAnswer">Whether this is the last answer of the AI, which the new chat keeps word for word.</param>
    /// <returns>The message as the AI reads it.</returns>
    public static string Render(ContentBlock block, int number, bool isLastAnswer = false)
    {
        var text = block.Content as ContentText;
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"[Message {number}, {RoleName(block.Role)}, {block.Time.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}");
        if (block.HideFromUser)
            sb.Append(", not shown to the user");

        if (isLastAnswer)
            sb.Append(", the last answer, which follows your summary word for word");

        sb.Append("]\n");
        sb.Append((text?.Text ?? string.Empty).Trim());
        sb.Append('\n');

        var attachments = (text?.FileAttachments ?? []).Select(Describe).ToList();
        if (attachments.Count > 0)
            sb.Append("Attached files: ").Append(string.Join(", ", attachments)).Append('\n');

        return sb.ToString();
    }

    /// <summary>
    /// Splits the messages into parts of at most the given number of tokens each.
    /// </summary>
    /// <remarks>
    /// Messages stay whole and in their order, so that a part never ends in the middle of one. A
    /// message which alone is larger than a part is cut in the middle and gets a part of its own:
    /// its beginning and its end are what a conversation refers to most.
    /// </remarks>
    /// <param name="messages">The rendered messages with their tokens, in the order of the conversation.</param>
    /// <param name="tokensPerPart">How many tokens of messages one part may hold.</param>
    /// <returns>The parts, each one the text of its messages. Empty without messages.</returns>
    public static IReadOnlyList<string> SplitIntoParts(IReadOnlyList<CountedMessage> messages, int tokensPerPart)
    {
        var parts = new List<string>();
        var start = 0;
        while (start < messages.Count)
        {
            var (part, next) = TakePart(messages, start, tokensPerPart);
            parts.Add(part);
            start = next;
        }

        return parts;
    }

    /// <summary>
    /// Takes the next part of the messages, as many as fit into the given number of tokens.
    /// </summary>
    /// <remarks>
    /// The summary asks for one part at a time, because the room for a part shrinks as the summary
    /// so far grows, which only shows after each answer. The rules are those of SplitIntoParts.
    /// </remarks>
    /// <param name="messages">The rendered messages with their tokens, in the order of the conversation.</param>
    /// <param name="start">The index of the first message of the part.</param>
    /// <param name="tokensPerPart">How many tokens of messages the part may hold.</param>
    /// <returns>The text of the part, and the index of the first message after it.</returns>
    internal static (string Text, int Next) TakePart(IReadOnlyList<CountedMessage> messages, int start, int tokensPerPart)
    {
        var part = new StringBuilder();
        var partTokens = 0;
        var index = start;
        while (index < messages.Count)
        {
            var text = messages[index].Text;
            var tokens = messages[index].Tokens;
            if (tokens > tokensPerPart)
            {
                // A message too large for any part gets a part of its own:
                if (part.Length > 0)
                    break;

                text = CutMiddle(text, tokens, tokensPerPart);
                tokens = tokensPerPart;
            }

            if (part.Length > 0 && partTokens + tokens > tokensPerPart)
                break;

            if (part.Length > 0)
                part.Append('\n');

            part.Append(text);
            partTokens += tokens;
            index++;
        }

        return (part.ToString(), index);
    }

    /// <summary>
    /// Cuts the middle out of a text, so that it takes about the given number of tokens.
    /// </summary>
    /// <remarks>
    /// The characters per token are those of the very text, which the counted tokens tell. Two thirds
    /// of what stays come from the beginning, one third from the end. Never cuts between the two
    /// halves of a surrogate pair.
    /// </remarks>
    /// <param name="text">The text.</param>
    /// <param name="tokens">How many tokens the whole text takes.</param>
    /// <param name="maxTokens">How many tokens the text may take.</param>
    /// <returns>The text when it fits, otherwise its beginning and its end with a marker in between.</returns>
    internal static string CutMiddle(string text, int tokens, int maxTokens)
    {
        if (tokens <= maxTokens || text.Length is 0)
            return text;

        var keep = Math.Max(0, (int)(text.Length * (Math.Max(0, maxTokens) / (double)tokens) * CUT_MARGIN));
        if (keep >= text.Length)
            return text;

        var headLength = keep * 2 / 3;
        if (headLength > 0 && char.IsHighSurrogate(text[headLength - 1]))
            headLength--;

        var tailStart = text.Length - (keep - headLength);
        if (tailStart < text.Length && char.IsLowSurrogate(text[tailStart]))
            tailStart++;

        var marker = string.Format(CultureInfo.InvariantCulture, LEFT_OUT_MARKER, tailStart - headLength);
        return string.Concat(text.AsSpan(0, headLength), $"\n{marker}\n", text.AsSpan(tailStart));
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