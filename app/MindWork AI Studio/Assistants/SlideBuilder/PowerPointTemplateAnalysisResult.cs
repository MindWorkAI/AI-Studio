namespace AIStudio.Assistants.SlideBuilder;

/// <summary>
/// Contains compatible template layouts or a user-safe validation issue.
/// </summary>
/// <param name="Analysis">The selected layouts.</param>
/// <param name="Issue">The issue when the template cannot be used.</param>
internal sealed record PowerPointTemplateAnalysisResult(
    PowerPointTemplateAnalysis? Analysis,
    string Issue);
