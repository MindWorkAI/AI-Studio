namespace AIStudio.Tools.Feedback;

/// <summary>
/// The predefined reasons a user can pick after a bad rating, so they don't have to type them out.
/// </summary>
/// <remarks>
/// The server receives the lowercase member names, e.g. <c>no_answer</c>, so renaming a member changes the CSV.
/// </remarks>
public enum FeedbackReason
{
    NONE,

    NO_ANSWER,
    SLOW_ANSWER,
    BAD_QUALITY,
    OTHER,
}