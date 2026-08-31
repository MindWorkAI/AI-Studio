namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Describes one model contribution displayed in the deterministic footer.
/// </summary>
/// <param name="Role">The semantic role fulfilled by the model.</param>
/// <param name="Model">The export-safe model name.</param>
public sealed record VisualBriefingModelContribution(
    VisualBriefingModelRole Role,
    string Model);