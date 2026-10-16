using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Identifies the JSON shape a content slot value must have.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingSlotType>))]
public enum VisualBriefingSlotType
{
    /// <summary>A JSON string, number, or boolean rendered as text.</summary>
    TEXT,
    
    /// <summary>A tabular object with columns and rows.</summary>
    TABLE,

    /// <summary>An ordered object containing chronological timeline items.</summary>
    TIMELINE,
}