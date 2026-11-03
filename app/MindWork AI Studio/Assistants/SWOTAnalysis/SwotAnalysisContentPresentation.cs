using System.Text.Json;

using AIStudio.Chat;

namespace AIStudio.Assistants.SWOTAnalysis;

internal static class SwotAnalysisContentPresentation
{
    /// <summary>
    /// Fails the reading of stored data which holds a null where the result expects a value, so that
    /// the result view never meets one.
    /// </summary>
    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        RespectNullableAnnotations = true,
    };

    public static ContentPresentation Create(SwotAnalysisResult result) =>
        ContentPresentation.Create(ContentPresentationKind.SWOT_ANALYSIS, result);

    /// <summary>
    /// Reads the SWOT analysis stored as the presentation of a text, so it can be shown as a matrix.
    /// </summary>
    /// <remarks>
    /// The data comes from a checked result, but a chat file may also be old or changed by hand.
    /// Whatever is not a complete analysis returns false, and the text is shown instead.
    /// </remarks>
    /// <param name="presentation">The presentation which may carry the analysis.</param>
    /// <param name="result">The analysis, or an empty one when there is none.</param>
    /// <returns>Whether the presentation carries a complete analysis.</returns>
    public static bool TryRead(ContentPresentation? presentation, out SwotAnalysisResult result)
    {
        result = new();
        if (presentation is not { Kind: ContentPresentationKind.SWOT_ANALYSIS, Data: { ValueKind: JsonValueKind.Object } data })
            return false;

        SwotAnalysisResult? candidate;
        try
        {
            candidate = data.Deserialize<SwotAnalysisResult>(JSON_OPTIONS);
        }
        catch (JsonException)
        {
            return false;
        }

        if (candidate is null || !IsComplete(candidate))
            return false;

        result = candidate;
        return true;
    }

    private static bool IsComplete(SwotAnalysisResult result) =>
        !string.IsNullOrWhiteSpace(result.MatrixHeading) &&
        !string.IsNullOrWhiteSpace(result.InternalLabel) &&
        !string.IsNullOrWhiteSpace(result.ExternalLabel) &&
        !string.IsNullOrWhiteSpace(result.PositiveLabel) &&
        !string.IsNullOrWhiteSpace(result.NegativeLabel) &&
        result.Categories.All(IsComplete) &&
        !string.IsNullOrWhiteSpace(result.OverallAssessment.Label) &&
        !string.IsNullOrWhiteSpace(result.OverallAssessment.Text) &&
        IsComplete(result.PrioritizedActions) &&
        IsComplete(result.ModerationNotes);

    private static bool IsComplete(SwotCategory category) =>
        !string.IsNullOrWhiteSpace(category.Label) &&
        (category.Findings.Count > 0 || !string.IsNullOrWhiteSpace(category.EmptyMessage)) &&
        category.Findings.All(IsComplete);

    private static bool IsComplete(SwotPrioritizedActions actions) =>
        !string.IsNullOrWhiteSpace(actions.Label) &&
        (actions.Items.Count > 0 || !string.IsNullOrWhiteSpace(actions.EmptyMessage)) &&
        actions.Items.All(IsComplete);

    private static bool IsComplete(SwotModerationNotes notes) =>
        !string.IsNullOrWhiteSpace(notes.Label) &&
        (notes.Items.Count > 0 || !string.IsNullOrWhiteSpace(notes.EmptyMessage)) &&
        notes.Items.All(IsComplete);

    // A list item may be null, since the nullable annotations of list items do not count when reading:
    private static bool IsComplete(SwotFinding? finding) =>
        finding is not null &&
        !string.IsNullOrWhiteSpace(finding.Summary) &&
        !string.IsNullOrWhiteSpace(finding.Explanation);

    private static bool IsComplete(SwotAction? action) =>
        action is not null &&
        !string.IsNullOrWhiteSpace(action.Action) &&
        !string.IsNullOrWhiteSpace(action.Rationale) &&
        action.AddressedFactors.Count > 0;

    private static bool IsComplete(SwotModerationNote? note) =>
        note is not null &&
        !string.IsNullOrWhiteSpace(note.Change) &&
        !string.IsNullOrWhiteSpace(note.Reason);
}