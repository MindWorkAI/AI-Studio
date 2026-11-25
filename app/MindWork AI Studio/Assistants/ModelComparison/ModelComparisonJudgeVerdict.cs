using System.Text.Json;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// What the judge said about the two answers, once it could be understood.
/// </summary>
/// <remarks>
/// Built from whatever the judge model answered, the same way ModelComparisonAnswer
/// is built from what the two compared models answered: a judge which did not answer, or whose
/// answer could not be read as the JSON it was asked for, ends up with Completed
/// false and nothing else to show.
/// </remarks>
public sealed record ModelComparisonJudgeVerdict
{
    /// <summary>
    /// The name to show for the judge model. Shown together with its verdict, once the user has
    /// voted -- the same point at which the names of the two compared models are revealed.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Whether the judge could be understood: it answered, and that answer held a preference in the
    /// expected shape.
    /// </summary>
    public required bool Completed { get; init; }

    /// <summary>
    /// Which column the judge preferred, or null when Completed is false. The same
    /// column terms the user's own vote uses, since the judge is asked about "Answer A"/"Answer B",
    /// never about a model.
    /// </summary>
    public ModelComparisonVote? Preferred { get; init; }

    /// <summary>
    /// Why the judge decided as it did, or an empty text when Completed is false.
    /// </summary>
    public string Reasoning { get; init; } = string.Empty;

    /// <summary>
    /// How the JSON of the judge is read: as forgiving as models write it, with comments and trailing commas.
    /// </summary>
    internal static readonly JsonSerializerOptions JSON_OPTIONS = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        TypeInfoResolver = ModelComparisonJsonContext.Default,
    }.AsReadOnly();

    /// <summary>
    /// Reads the judge's answer. Never throws: whatever cannot be understood becomes a verdict
    /// which is not Completed, the same as a judge which did not answer at all.
    /// </summary>
    /// <param name="label">The name of the judge model.</param>
    /// <param name="judgeAnswered">Whether the judge model answered at all, before its text is even looked at.</param>
    /// <param name="judgeText">What the judge model answered.</param>
    public static ModelComparisonJudgeVerdict Parse(string label, bool judgeAnswered, string judgeText) => Parse(label, judgeAnswered, judgeText, ModelComparisonJudgeFormat.ANSWERS);

    /// <summary>
    /// Reads the judge's answer about the two things a format names, e.g., two chats. Never throws, see the other
    /// overload.
    /// </summary>
    /// <param name="label">The name of the judge model.</param>
    /// <param name="judgeAnswered">Whether the judge model answered at all, before its text is even looked at.</param>
    /// <param name="judgeText">What the judge model answered.</param>
    /// <param name="format">How the judge was asked to name the two, see ModelComparisonJudgeFormat.</param>
    public static ModelComparisonJudgeVerdict Parse(string label, bool judgeAnswered, string judgeText, ModelComparisonJudgeFormat format)
    {
        var notCompleted = new ModelComparisonJudgeVerdict { Label = label, Completed = false };
        if (!judgeAnswered)
            return notCompleted;

        var json = ModelAnswerJson.ExtractObject(judgeText);
        if (json.IsWhiteSpace())
            return notCompleted;

        try
        {
            var parsed = JsonSerializer.Deserialize<ModelComparisonJudgeAnswer>(json, JSON_OPTIONS);
            if (parsed is null || format.VoteOf(parsed.Preferred) is not { } preference)
                return notCompleted;

            return new ModelComparisonJudgeVerdict
            {
                Label = label,
                Completed = true,
                Preferred = preference,
                Reasoning = parsed.Reasoning?.Trim() ?? string.Empty,
            };
        }
        catch (JsonException)
        {
            return notCompleted;
        }
    }
}
