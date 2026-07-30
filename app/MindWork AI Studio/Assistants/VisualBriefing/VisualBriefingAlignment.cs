using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Identifies an allowed cross-axis alignment in the presentation layout.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingAlignment>))]
public enum VisualBriefingAlignment
{
    /// <summary>Aligns content at the start edge.</summary>
    START,
    
    /// <summary>Centers content.</summary>
    CENTER,
    
    /// <summary>Aligns content at the end edge.</summary>
    END,
    
    /// <summary>Stretches content across the available space.</summary>
    STRETCH,
}