using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Selects the desktop presentation direction of a chronological timeline.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingTimelineOrientation>))]
public enum VisualBriefingTimelineOrientation
{
    /// <summary>Places timeline items along a horizontal track on sufficiently wide screens.</summary>
    HORIZONTAL,

    /// <summary>Places timeline items along a vertical track.</summary>
    VERTICAL,
}