namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// What the editor of the Visual Briefing assistant hands to the store, so that it notices any change worth saving.
/// </summary>
/// <param name="Name">The name of the briefing.</param>
/// <param name="Author">The author of the briefing.</param>
/// <param name="Settings">The settings of the briefing.</param>
/// <param name="Sources">The sources of the briefing.</param>
internal sealed record VisualBriefingPersistenceFingerprint(string Name, string Author, VisualBriefingLocalSettings Settings, VisualBriefingSourceFingerprint[] Sources);