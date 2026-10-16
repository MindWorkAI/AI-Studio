using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines one node in the validated bounded presentation layout tree.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[CanonicalJsonShape("14064835")]
public sealed class VisualBriefingLayoutNode
{
    /// <summary>Gets or sets the globally unique layout node identifier.</summary>
    [JsonRequired]
    public string NodeId { get; init; } = string.Empty;

    /// <summary>Gets or sets the node kind.</summary>
    [JsonRequired]
    public VisualBriefingLayoutNodeKind Kind { get; init; }

    /// <summary>Gets or sets the planned section identifier for a section node.</summary>
    [JsonRequired]
    public string? SectionId { get; init; }

    /// <summary>Gets or sets the planned component identifier for a component node.</summary>
    [JsonRequired]
    public string? ComponentId { get; init; }

    /// <summary>Gets or sets the ordered child nodes.</summary>
    [JsonRequired]
    public List<VisualBriefingLayoutNode> Children { get; init; } = [];

    /// <summary>Gets or sets responsive columns for a grid node.</summary>
    [JsonRequired]
    public VisualBriefingResponsiveColumns? Columns { get; set; }

    /// <summary>Gets or sets the bounded grid span.</summary>
    [JsonRequired]
    public int Span { get; set; } = 1;

    /// <summary>Gets or sets the explicit sibling order.</summary>
    [JsonRequired]
    public int Order { get; init; }

    /// <summary>Gets or sets whether the node receives visual emphasis.</summary>
    [JsonRequired]
    public bool Emphasized { get; set; }

    /// <summary>Gets or sets the cross-axis alignment.</summary>
    [JsonRequired]
    public VisualBriefingAlignment Alignment { get; set; }
}