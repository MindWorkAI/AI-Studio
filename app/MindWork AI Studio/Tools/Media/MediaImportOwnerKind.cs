namespace AIStudio.Tools.Media;

/// <summary>Supported persistent media-operation owners.</summary>
public enum MediaImportOwnerKind
{
    CHAT,
    ASSISTANT,

    /// <summary>
    /// Identifies persistent media transcripts owned by a visual briefing.
    /// </summary>
    VISUAL_BRIEFING,
}