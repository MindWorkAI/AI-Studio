namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Overrides visual briefing storage for focused tests and isolated hosts.
/// </summary>
public sealed class VisualBriefingStorageOptions
{
    /// <summary>
    /// Gets or initializes the directory in which the visualBriefings folder is created.
    /// </summary>
    public string? DataDirectory { get; init; }
}
