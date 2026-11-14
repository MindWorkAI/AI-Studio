namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// How much the user had to do with a model of a comparison series until the task was done.
/// </summary>
public sealed record ModelComparisonSeriesEffort
{
    /// <summary>
    /// The model.
    /// </summary>
    public required ModelComparisonModelIdentity Model { get; init; }

    /// <summary>
    /// How many decided tasks count.
    /// </summary>
    public int TaskCount { get; init; }

    /// <summary>
    /// How many messages the user wrote on average, the first one included, or null without a decided task.
    /// </summary>
    public double? AverageUserMessages { get; init; }

    /// <summary>
    /// How long the user worked with the model in the middle: half of the tasks took less, half took more. The median,
    /// since a chat left open over night would distort an average. Null without a decided task.
    /// </summary>
    public TimeSpan? MedianDuration { get; init; }
}