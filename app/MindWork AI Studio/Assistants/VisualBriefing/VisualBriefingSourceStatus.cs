using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines <c>VisualBriefingSourceStatus</c> for the visual briefing feature.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingSourceStatus>))]
public enum VisualBriefingSourceStatus
{
    /// <summary>
    /// Defines <c>UNCHANGED</c> for the visual briefing feature.
    /// </summary>
    UNCHANGED,
    /// <summary>
    /// Defines <c>CHANGED</c> for the visual briefing feature.
    /// </summary>
    CHANGED,
    /// <summary>
    /// Defines <c>TRANSCRIPT_OUTDATED</c> for the visual briefing feature.
    /// </summary>
    TRANSCRIPT_OUTDATED,
    /// <summary>
    /// Defines <c>UNREACHABLE</c> for the visual briefing feature.
    /// </summary>
    UNREACHABLE,
}