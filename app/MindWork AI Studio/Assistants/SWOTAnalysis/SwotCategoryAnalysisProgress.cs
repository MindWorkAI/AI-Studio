namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// How far the analysis of one SWOT category is.
/// </summary>
/// <remarks>
/// The assistant keeps one progress per category in its session state, whose snapshot copies the
/// dictionary but compares its entries by reference. Therefore, replace an entry with a new progress
/// and never change one in place: a change in place would not reach an instance the user returns to.
/// </remarks>
/// <param name="Status">Whether the analysis is running, completed, failed, or canceled.</param>
/// <param name="Result">The findings of the category, once the analysis is completed.</param>
/// <param name="Message">Why the analysis failed or stopped, for the user.</param>
internal sealed record SwotCategoryAnalysisProgress(
    SwotCategoryAnalysisStatus Status,
    SwotCategoryAnalysisResult? Result = null,
    string Message = "");