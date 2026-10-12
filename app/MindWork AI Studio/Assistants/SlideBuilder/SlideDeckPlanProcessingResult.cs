namespace AIStudio.Assistants.SlideBuilder;

/// <summary>
/// Contains a validated slide plan or the safe reason why the model response was rejected.
/// </summary>
/// <param name="Plan">The validated plan.</param>
/// <param name="Issue">The content-free rejection description.</param>
/// <param name="Diagnostic">The structural diagnostic used for one repair attempt.</param>
internal sealed record SlideDeckPlanProcessingResult(
    SlideDeckPlan? Plan,
    string Issue,
    StructuredResponseDiagnostic? Diagnostic);
