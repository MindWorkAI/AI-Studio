using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines <c>VisualBriefingPreviewDevice</c> for the visual briefing feature.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingPreviewDevice>))]
public enum VisualBriefingPreviewDevice
{
    /// <summary>
    /// Defines <c>DESKTOP</c> for the visual briefing feature.
    /// </summary>
    DESKTOP,
    /// <summary>
    /// Defines <c>TABLET</c> for the visual briefing feature.
    /// </summary>
    TABLET,
    /// <summary>
    /// Defines <c>MOBILE</c> for the visual briefing feature.
    /// </summary>
    MOBILE,
}