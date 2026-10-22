namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// What one model answered, together with how the request went.
/// </summary>
public sealed record ModelComparisonAnswer
{
    /// <summary>
    /// The name to show for the model. It says who wrote the answer, and is not shown until the
    /// user has voted.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// The answer without the thinking of the model. Only an answer which is
    /// <see cref="Completed"/> is complete: after a failure or a cancellation, this is whatever
    /// arrived before, and it must not be shown as a result.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Whether the model answered. False when the request failed, when the user canceled it, and
    /// when the model ended without saying anything.
    /// </summary>
    public required bool Completed { get; init; }

    /// <summary>
    /// The time from sending the request until the first piece of the answer arrived, or null when
    /// nothing arrived.
    /// </summary>
    public TimeSpan? FirstTokenTime { get; init; }

    /// <summary>
    /// The time from sending the request until the answer was complete, or null when it never was.
    /// </summary>
    public TimeSpan? TotalTime { get; init; }
}