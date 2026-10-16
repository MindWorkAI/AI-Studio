using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines bounded responsive column counts for one grid layout node.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[CanonicalJsonShape("92c96e68")]
public sealed class VisualBriefingResponsiveColumns
{
    /// <summary>Gets or sets the mobile column count.</summary>
    [JsonRequired]
    public int Mobile { get; set; } = 1;

    /// <summary>Gets or sets the tablet column count.</summary>
    [JsonRequired]
    public int Tablet { get; set; } = 1;

    /// <summary>Gets or sets the desktop column count.</summary>
    [JsonRequired]
    public int Desktop { get; set; } = 1;
}