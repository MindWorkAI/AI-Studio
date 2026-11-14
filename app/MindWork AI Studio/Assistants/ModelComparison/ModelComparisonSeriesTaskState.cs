namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// How far a task of a comparison series got.
/// </summary>
public enum ModelComparisonSeriesTaskState
{
    /// <summary>
    /// The user works on the task in the first chat.
    /// </summary>
    FIRST_CHAT,

    /// <summary>
    /// The user works on the task in the second chat, which started with the same first message.
    /// </summary>
    SECOND_CHAT,

    /// <summary>
    /// The user is done with both chats and decides which one helped more.
    /// </summary>
    DECIDING,

    /// <summary>
    /// The user decided; the task counts toward the result of the series.
    /// </summary>
    DONE,
}