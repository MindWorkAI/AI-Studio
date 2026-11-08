namespace AIStudio.Chat.Compression;

/// <summary>
/// What compressing a chat does with each of its blocks.
/// </summary>
/// <remarks>
/// Compressing never changes the chat itself. The conversation continues in a new chat, which gets
/// the example conversation of the chat template, a summary of everything in between, and the last
/// exchange word for word: the last message of the user, with its files, and the last answer of the
/// AI. The last exchange stays because the next message of the user mostly refers to it. The summary
/// goes into the last message of the user, ahead of its text, see ChatCompression.TurnIntoContinuation:
/// as a message of its own, it would follow the example conversation and precede the last message,
/// so two messages of the user would meet, which the chat templates of some local models refuse.<br/><br/>
///
/// A last message of the user which is hidden, such as the prompt an assistant sent into the chat,
/// is summarized instead: the new chat could show it only together with the summary, which the user
/// has to see. The summary then stands as a message of its own, right before the last answer.<br/><br/>
///
/// A message of the user which never got an answer, e.g., because the chat had grown too long for
/// the model, is not summarized as if it had been answered. It goes into the field for the next
/// message of the new chat instead, so the user can send it there. When the user already started
/// another message, that one wins, and the unanswered message is summarized with the rest.
/// </remarks>
/// <param name="Preamble">The example conversation of the chat template, which the new chat starts with unchanged.</param>
/// <param name="ToSummarize">The messages the summary replaces, in the order of the conversation.</param>
/// <param name="LastQuestion">The last message of the user before the last answer, which the new chat keeps word for word with its files, or null when it is hidden and therefore summarized.</param>
/// <param name="LastAnswer">The last answer of the AI, which the new chat keeps word for word.</param>
/// <param name="PendingQuestion">The last message of the user when it has no answer and goes into the field for the next message of the new chat, otherwise null.</param>
public sealed record ChatCompressionPlan(IReadOnlyList<ContentBlock> Preamble, IReadOnlyList<ContentBlock> ToSummarize, ContentBlock? LastQuestion, ContentBlock LastAnswer, ContentText? PendingQuestion)
{
    /// <summary>
    /// Plans how to compress the given chat.
    /// </summary>
    /// <remarks>
    /// The order of the conversation is the one of the time stamps, as everywhere the chat is shown
    /// or rolled back. Only messages which the message builder sends count, that is, those with a
    /// text: a block without one never reached the AI, so there is nothing to summarize in it.<br/><br/>
    ///
    /// The example conversation of the chat template is recognized as the hidden blocks the chat
    /// starts with, at most as many as the template holds. A chat whose template changed since keeps
    /// what it actually starts with; hidden blocks beyond that, such as the prompts an assistant
    /// sent into the chat, are part of the conversation and get summarized.
    /// </remarks>
    /// <param name="chat">The chat to compress. It stays unchanged.</param>
    /// <param name="exampleConversationLength">How many blocks the example conversation of the chat template holds, 0 without a template.</param>
    /// <param name="draftIsEmpty">Whether the user has not started a message of their own in the chat.</param>
    /// <returns>The plan, or null when the chat holds no finished answer of the AI, or nothing to summarize besides the last exchange.</returns>
    public static ChatCompressionPlan? Of(ChatThread chat, int exampleConversationLength, bool draftIsEmpty)
    {
        var blocks = chat.Blocks.OrderBy(block => block.Time).ToList();

        var preambleLength = 0;
        while (preambleLength < exampleConversationLength && preambleLength < blocks.Count && blocks[preambleLength].HideFromUser)
            preambleLength++;

        var lastAnswerIndex = blocks.FindLastIndex(IsFinishedAnswer);
        if (lastAnswerIndex < preambleLength)
            return null;

        var beforeTheAnswer = blocks[preambleLength..lastAnswerIndex];
        var lastQuestion = beforeTheAnswer.LastOrDefault(block => block.Role is ChatRole.USER && IsSentText(block));
        if (lastQuestion is { HideFromUser: true })
            lastQuestion = null;

        var afterTheAnswer = blocks[(lastAnswerIndex + 1)..];
        var pendingBlock = draftIsEmpty
            ? afterTheAnswer.LastOrDefault(block => block is { Role: ChatRole.USER, HideFromUser: false } && IsSentText(block))
            : null;

        var toSummarize = beforeTheAnswer
            .Where(block => !ReferenceEquals(block, lastQuestion))
            .Concat(afterTheAnswer.Where(block => !ReferenceEquals(block, pendingBlock)))
            .Where(IsSentText)
            .ToList();

        if (toSummarize.Count is 0)
            return null;

        return new(blocks[..preambleLength], toSummarize, lastQuestion, blocks[lastAnswerIndex], pendingBlock?.Content as ContentText);
    }

    /// <summary>
    /// Whether the chat holds an answer of the AI which compressing could keep.
    /// </summary>
    /// <remarks>
    /// Cheap enough to be asked with every rendering of the chat, unlike planning, which sorts the
    /// whole conversation.
    /// </remarks>
    /// <param name="chat">The chat.</param>
    /// <returns>True when there is a finished answer of the AI.</returns>
    public static bool HasFinishedAnswer(ChatThread chat) => chat.Blocks.Any(IsFinishedAnswer);

    /// <summary>
    /// Whether a block becomes a message when the request is built, see ListContentBlockExtensions.BuildMessagesAsync.
    /// </summary>
    internal static bool IsSentText(ContentBlock block) => block is { ContentType: ContentType.TEXT, Content: ContentText { Text: var text } } && !string.IsNullOrWhiteSpace(text);

    /// <summary>
    /// Whether a block is an answer of the AI which the new chat could keep and show.
    /// </summary>
    /// <remarks>
    /// Hidden answers do not count, such as the example answers of a chat template: the user could
    /// not see what the new chat continues from.
    /// </remarks>
    private static bool IsFinishedAnswer(ContentBlock block) => block is { Role: ChatRole.AI, HideFromUser: false, Content: ContentText { IsStreaming: false } } && IsSentText(block);
}