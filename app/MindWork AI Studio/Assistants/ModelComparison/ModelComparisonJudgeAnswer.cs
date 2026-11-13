namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// The JSON object the judge is asked to answer with, as it arrives, before it is checked.
/// </summary>
/// <remarks>
/// Both values may be missing or null in what a model writes: ModelComparisonJudgeVerdict.Parse decides
/// what a usable verdict is, not the reading of the JSON.
/// </remarks>
public sealed record ModelComparisonJudgeAnswer
{
    /// <summary>
    /// The column the judge preferred: "A", "B", or "TIE".
    /// </summary>
    public string? Preferred { get; set; }

    /// <summary>
    /// Why the judge decided as it did.
    /// </summary>
    public string? Reasoning { get; set; }
}