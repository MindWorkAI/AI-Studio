using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Identifies the semantic purpose of one content slot.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingSlotRole>))]
public enum VisualBriefingSlotRole
{
    /// <summary>Provides a short contextual label above a title.</summary>
    EYEBROW,
    
    /// <summary>Provides a heading.</summary>
    TITLE,
    
    /// <summary>Provides a concise synopsis.</summary>
    SUMMARY,
    
    /// <summary>Provides primary narrative copy.</summary>
    BODY,
    
    /// <summary>Names a value, control, or panel.</summary>
    LABEL,
    
    /// <summary>Provides a highlighted value.</summary>
    VALUE,
    
    /// <summary>Explains or qualifies a value.</summary>
    CONTEXT,
    
    /// <summary>Provides a caption for a visual or table.</summary>
    CAPTION,
    
    /// <summary>Provides the structured rows and columns of a table.</summary>
    TABLE_DATA,
    
    /// <summary>Provides content for one interactive panel.</summary>
    PANEL,
    
    /// <summary>Provides a calculated simulation result.</summary>
    RESULT,

    /// <summary>Provides the ordered entries of a chronological timeline.</summary>
    TIMELINE_DATA,
}