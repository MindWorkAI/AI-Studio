namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Describes whether a persisted visual briefing can be opened by this AI Studio version.
/// </summary>
internal enum VisualBriefingProjectLoadStatus
{
    AVAILABLE,
    NEWER_VERSION,
    UNAVAILABLE,
}