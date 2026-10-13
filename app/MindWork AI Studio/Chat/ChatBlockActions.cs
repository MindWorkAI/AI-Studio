using System.Diagnostics.CodeAnalysis;

namespace AIStudio.Chat;

/// <summary>
/// Changes the messages of a chat on behalf of the user, but never while an answer is being written for it.
/// </summary>
/// <remarks>
/// While an answer is being written, the AI job holds the very same thread as the chat: it writes into
/// the answer, and it stores the whole conversation every few seconds. A message the user removed in
/// the meantime does not come back, but the job goes on writing an answer nobody sees, and the chat
/// stays locked until that answer is complete. Removing the answer and sending another message would
/// leave two answers being written for one chat. Changing older messages is no better: the job stores
/// the conversation on another thread and could read it in the middle of the change.<br/><br/>
///
/// That is why every message is locked while an answer is being written, not only the answer itself.
/// Stopping the answer unlocks the chat right away.
/// </remarks>
/// <param name="isAnswerBeingWritten">Whether an answer is being written for the chat with the given ID right now.</param>
public sealed class ChatBlockActions(Func<Guid, bool> isAnswerBeingWritten)
{
    /// <summary>
    /// Whether the user may change the messages of the given chat right now.
    /// </summary>
    /// <param name="chat">The chat, or null when there is none yet.</param>
    /// <returns>True when there is a chat and no answer is being written for it.</returns>
    public bool AreAllowed([NotNullWhen(true)] ChatThread? chat) => chat is not null && !isAnswerBeingWritten(chat.ChatId);

    /// <summary>
    /// Removes a message, together with the hidden prompt an answer was written for.
    /// </summary>
    /// <param name="chat">The chat.</param>
    /// <param name="content">The content of the message to remove.</param>
    /// <returns>True when the message was removed.</returns>
    public bool TryRemove([NotNullWhen(true)] ChatThread? chat, IContent content)
    {
        if (!this.AreAllowed(chat) || !Holds(chat, content))
            return false;

        chat.Remove(content);
        return true;
    }

    /// <summary>
    /// Removes an answer so that it can be written anew for the same question.
    /// </summary>
    /// <param name="chat">The chat.</param>
    /// <param name="answer">The content of the answer to remove.</param>
    /// <returns>True when the answer was removed.</returns>
    public bool TryRemoveForRegenerate([NotNullWhen(true)] ChatThread? chat, IContent answer)
    {
        if (!this.AreAllowed(chat) || !Holds(chat, answer))
            return false;

        chat.Remove(answer, removeForRegenerate: true);
        return true;
    }

    /// <summary>
    /// Rolls the chat back, so that the given answer becomes its last message.
    /// </summary>
    /// <remarks>
    /// What the rollback removes and what it keeps is documented at ChatThread.RollBackTo.
    /// </remarks>
    /// <param name="chat">The chat.</param>
    /// <param name="answer">The content of the answer to keep as the last message.</param>
    /// <returns>True when later messages were removed.</returns>
    public bool TryRollBack([NotNullWhen(true)] ChatThread? chat, IContent answer) => this.AreAllowed(chat) && chat.RollBackTo(answer);

    /// <summary>
    /// Makes the last question the draft again, and removes the answer to it.
    /// </summary>
    /// <param name="chat">The chat.</param>
    /// <param name="question">The content of the question to edit.</param>
    /// <returns>True when the question became the draft.</returns>
    public bool TryEditLastUserBlock([NotNullWhen(true)] ChatThread? chat, IContent question)
    {
        if (!this.AreAllowed(chat) || question is not ContentText || !Holds(chat, question))
            return false;

        var lastContent = chat.Blocks.Last().Content;
        chat.RemoveForEditing(question);
        if (lastContent is not null)
            chat.Remove(lastContent);

        return true;
    }

    /// <summary>
    /// Makes a question without an answer the draft again.
    /// </summary>
    /// <param name="chat">The chat.</param>
    /// <param name="question">The content of the question to edit.</param>
    /// <returns>True when the question became the draft.</returns>
    public bool TryEditLastBlock([NotNullWhen(true)] ChatThread? chat, IContent question)
    {
        if (!this.AreAllowed(chat) || question is not ContentText || !Holds(chat, question))
            return false;

        chat.RemoveForEditing(question);
        return true;
    }

    private static bool Holds(ChatThread chat, IContent content) => chat.Blocks.Any(block => block.Content == content);
}