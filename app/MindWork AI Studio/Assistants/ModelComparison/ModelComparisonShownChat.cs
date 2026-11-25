namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// The chat of a task which the user works in right now, with what it needs before it shows.
/// </summary>
/// <param name="ChatId">The ID of the chat.</param>
/// <param name="Provider">The provider which answers in the chat.</param>
/// <param name="OpensWithFirstMessage">Whether the chat starts with a copy of the first message of the first chat, which holds for the second chat.</param>
public sealed record ModelComparisonShownChat(Guid ChatId, AIStudio.Settings.Provider Provider, bool OpensWithFirstMessage)
{
    /// <summary>
    /// Tells which chat of a task the user works in, and which model answers there.
    /// </summary>
    /// <param name="task">The task.</param>
    /// <param name="firstProvider">The provider of the first model of the series.</param>
    /// <param name="secondProvider">The provider of the second model of the series.</param>
    /// <returns>The chat, or null while the user decides or once the task is done.</returns>
    public static ModelComparisonShownChat? Of(ModelComparisonSeriesTask task, AIStudio.Settings.Provider firstProvider, AIStudio.Settings.Provider secondProvider)
    {
        if (task.CurrentChat is not { } chat)
            return null;

        // The first model answers in the first chat when it was drawn for it, and in the second chat otherwise:
        var isFirstChat = task.State is ModelComparisonSeriesTaskState.FIRST_CHAT;
        var provider = isFirstChat == task.FirstModelIsChatOne ? firstProvider : secondProvider;
        return new(chat.ChatId, provider, !isFirstChat);
    }
}