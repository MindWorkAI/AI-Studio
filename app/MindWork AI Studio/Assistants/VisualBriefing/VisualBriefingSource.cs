using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines <c>VisualBriefingSource</c> for the visual briefing feature.
/// </summary>
public sealed class VisualBriefingSource
{
    /// <summary>
    /// Defines <c>SourceId</c> for the visual briefing feature.
    /// </summary>
    public Guid SourceId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Defines <c>Kind</c> for the visual briefing feature.
    /// </summary>
    public VisualBriefingSourceKind Kind { get; set; }

    /// <summary>
    /// Defines <c>Path</c> for the visual briefing feature.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Defines <c>Size</c> for the visual briefing feature.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Defines <c>LastWriteTimeUtc</c> for the visual briefing feature.
    /// </summary>
    public DateTimeOffset LastWriteTimeUtc { get; set; }

    /// <summary>
    /// Defines <c>TranscriptStatus</c> for the visual briefing feature.
    /// </summary>
    public VisualBriefingTranscriptStatus TranscriptStatus { get; set; } = VisualBriefingTranscriptStatus.NOT_REQUIRED;

    /// <summary>
    /// Defines <c>IsMedia</c> for the visual briefing feature.
    /// </summary>
    public bool IsMedia { get; set; }

    /// <summary>
    /// Defines <c>AssetId</c> for the visual briefing feature.
    /// </summary>
    public string AssetId { get; set; } = string.Empty;

    /// <summary>
    /// Defines <c>Status</c> for the visual briefing feature.
    /// </summary>
    [JsonIgnore]
    public VisualBriefingSourceStatus Status { get; set; } = VisualBriefingSourceStatus.UNCHANGED;
}