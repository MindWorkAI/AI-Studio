using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines <c>VisualBriefingSourceKind</c> for the visual briefing feature.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingSourceKind>))]
public enum VisualBriefingSourceKind
{
    /// <summary>
    /// Defines <c>SOURCE_MATERIAL</c> for the visual briefing feature.
    /// </summary>
    SOURCE_MATERIAL,
    /// <summary>
    /// Defines <c>VISUAL_ASSET</c> for the visual briefing feature.
    /// </summary>
    VISUAL_ASSET,
}