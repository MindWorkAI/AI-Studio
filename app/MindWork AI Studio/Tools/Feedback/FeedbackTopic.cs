namespace AIStudio.Tools.Feedback;

/// <summary>
/// The topics a user can pick for their comment, so the comments can be grouped by topic.
/// </summary>
/// <remarks>
/// The server receives the lowercase member names, e.g. <c>confusing_ui</c>, so renaming a member changes the CSV.
/// </remarks>
public enum FeedbackTopic
{
    NONE,

    TECHNICAL_PROBLEMS,
    NEW_FEATURE,
    MISSING_FUNCTIONS,
    CONFUSING_UI,
    OTHER,
}