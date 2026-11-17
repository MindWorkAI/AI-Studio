namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// The design of the previous revision, which the presentation stage shows its model when the design changes.
/// </summary>
/// <param name="Layout">The layout tree of the previous revision.</param>
/// <param name="Profile">The design profile of the previous revision.</param>
internal sealed record VisualBriefingParentDesign(VisualBriefingLayoutNode Layout, VisualBriefingDesignProfile Profile);