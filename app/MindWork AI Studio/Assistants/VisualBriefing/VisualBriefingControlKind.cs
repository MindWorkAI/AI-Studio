using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Identifies a declarative interaction control supported by the briefing runtime.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingControlKind>))]
public enum VisualBriefingControlKind
{
    /// <summary>Selects one tab panel.</summary>
    TAB,
    
    /// <summary>Filters a component by one value.</summary>
    FILTER,
    
    /// <summary>Accepts a numeric value.</summary>
    NUMBER,
    
    /// <summary>Accepts a numeric value within a range.</summary>
    RANGE,
    
    /// <summary>Selects one option from a list.</summary>
    SELECT,
}