namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Describes one model contribution displayed in the deterministic footer.
/// </summary>
public sealed record VisualBriefingModelContribution(
    VisualBriefingModelRole Role,
    string Model);