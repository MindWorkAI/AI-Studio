namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// How a model did in one run of a stored comparison: whether it answered, and how long it took. The answer itself is
/// not stored.
/// </summary>
public sealed record ModelComparisonRecordAnswer
{
    /// <summary>
    /// Whether the model answered.
    /// </summary>
    public bool Answered { get; set; }

    /// <summary>
    /// The time until the first piece of the answer arrived, or null when nothing arrived.
    /// </summary>
    public TimeSpan? FirstTokenTime { get; set; }

    /// <summary>
    /// The time until the answer was complete, or null when it never was.
    /// </summary>
    public TimeSpan? TotalTime { get; set; }

    /// <summary>
    /// Gets what a stored comparison keeps of an answer.
    /// </summary>
    /// <param name="answer">The answer.</param>
    /// <returns>Whether it was complete, and its times.</returns>
    public static ModelComparisonRecordAnswer Of(ModelComparisonAnswer answer) => new()
    {
        Answered = answer.Completed,
        FirstTokenTime = answer.FirstTokenTime,
        TotalTime = answer.TotalTime,
    };
}