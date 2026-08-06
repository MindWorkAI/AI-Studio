using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines <c>VisualBriefingTranscriptStatus</c> for the visual briefing feature.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingTranscriptStatus>))]
public enum VisualBriefingTranscriptStatus
{
    /// <summary>
    /// Defines <c>NOT_REQUIRED</c> for the visual briefing feature.
    /// </summary>
    NOT_REQUIRED,
    /// <summary>
    /// Defines <c>CURRENT</c> for the visual briefing feature.
    /// </summary>
    CURRENT,
    /// <summary>
    /// Defines <c>OUTDATED</c> for the visual briefing feature.
    /// </summary>
    OUTDATED,
    /// <summary>
    /// Defines <c>MISSING</c> for the visual briefing feature.
    /// </summary>
    MISSING,
}