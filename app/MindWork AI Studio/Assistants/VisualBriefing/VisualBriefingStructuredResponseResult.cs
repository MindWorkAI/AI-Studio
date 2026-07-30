namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Contains a parsed structured response or its safe rejection.
/// </summary>
/// <typeparam name="T">The strict response type.</typeparam>
/// <param name="Response">The fully validated response.</param>
/// <param name="Issue">The safe rejection.</param>
internal sealed record VisualBriefingStructuredResponseResult<T>(T? Response, VisualBriefingContractIssue? Issue) where T : class;