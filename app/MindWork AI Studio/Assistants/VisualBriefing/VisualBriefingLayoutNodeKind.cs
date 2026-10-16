using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Identifies the function of a node in the bounded presentation layout tree.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingLayoutNodeKind>))]
public enum VisualBriefingLayoutNodeKind
{
    /// <summary>Represents one planned semantic section.</summary>
    SECTION,
    
    /// <summary>Arranges child nodes in a vertical sequence.</summary>
    STACK,
    
    /// <summary>Arranges child nodes in responsive columns.</summary>
    GRID,
    
    /// <summary>Places one planned component.</summary>
    COMPONENT,
}