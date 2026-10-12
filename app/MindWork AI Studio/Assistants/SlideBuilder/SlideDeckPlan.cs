using System.Text.Json.Serialization;

namespace AIStudio.Assistants.SlideBuilder;

/// <summary>
/// Defines the strict model response from which a PowerPoint presentation is rendered.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SlideDeckPlan
{
    /// <summary>Gets or sets the response contract version.</summary>
    [JsonRequired]
    public int ContractVersion { get; set; }

    /// <summary>Gets or sets the presentation title used on the title slide.</summary>
    [JsonRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the content slides following the title slide.</summary>
    [JsonRequired]
    public List<SlideDeckPlanSlide> Slides { get; set; } = [];
}
