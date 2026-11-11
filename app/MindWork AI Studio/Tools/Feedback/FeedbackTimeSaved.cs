namespace AIStudio.Tools.Feedback;

/// <summary>
/// The answers to the question whether AI Studio saved the user time this week, a scale of five
/// with "don't know" in the middle.
/// </summary>
/// <remarks>
/// The server receives the lowercase member names, e.g. <c>saved_some</c>, so renaming a member changes the CSV.
/// </remarks>
public enum FeedbackTimeSaved
{
    NONE,

    LOST,
    NOT_SAVED,
    UNKNOWN,
    SAVED_SOME,
    SAVED_MUCH,
}