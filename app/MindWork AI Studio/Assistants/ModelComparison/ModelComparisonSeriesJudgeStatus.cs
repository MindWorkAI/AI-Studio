namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// What became of the judge for a task of a comparison series.
/// </summary>
public enum ModelComparisonSeriesJudgeStatus
{
    /// <summary>
    /// The judge was not asked: the series has none, the user did not decide yet, or the user left before the judge
    /// answered.
    /// </summary>
    NOT_ASKED,

    /// <summary>
    /// The judge gave a usable verdict.
    /// </summary>
    VERDICT,

    /// <summary>
    /// The judge was asked, but gave no usable verdict.
    /// </summary>
    NO_VERDICT,

    /// <summary>
    /// Both chats together are too long for the context window of the judge, so it was not asked.
    /// </summary>
    TOO_LONG,

    /// <summary>
    /// The judge is no longer available as it was when the series began, so it was not asked.
    /// </summary>
    UNAVAILABLE,
}