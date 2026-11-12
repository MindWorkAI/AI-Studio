namespace AIStudio.Settings.DataModel;

/// <summary>
/// Remembers the weekly feedback of the chat across app sessions.
/// </summary>
public sealed class DataFeedback
{
    /// <summary>
    /// The Friday of the last week the user rated or skipped. The chat asks again once the next Friday has come.
    /// </summary>
    public DateOnly LastHandledWeek { get; set; } = DateOnly.MinValue;

    /// <summary>
    /// When the user last rated or skipped. Only the short debug interval uses it.
    /// </summary>
    public DateTime LastHandledAt { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Debug builds only: ask every few minutes instead of once a week, to test the prompt.
    /// </summary>
    public bool DebugShortInterval { get; set; }
}