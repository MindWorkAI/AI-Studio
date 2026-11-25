namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// How fast a model answered across all stored comparisons.
/// </summary>
/// <remarks>
/// Medians rather than means: a single answer which waited a minute for a busy server would otherwise shift the whole
/// picture. The times depend on the length of the answers and the load of the server as much as on the model, so they
/// compare models only when both ran on the same tasks.
/// </remarks>
public sealed record ModelComparisonModelSpeed
{
    /// <summary>
    /// The model, with the name it had in its latest comparison.
    /// </summary>
    public required ModelComparisonModelIdentity Model { get; init; }

    /// <summary>
    /// How many complete answers the times come from.
    /// </summary>
    public int AnswerCount { get; init; }

    /// <summary>
    /// The median time until the first piece of an answer arrived, or null without any.
    /// </summary>
    public TimeSpan? MedianFirstTokenTime { get; init; }

    /// <summary>
    /// The median time until an answer was complete, or null without any.
    /// </summary>
    public TimeSpan? MedianTotalTime { get; init; }
}