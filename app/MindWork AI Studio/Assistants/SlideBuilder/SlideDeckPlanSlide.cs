using System.Text.Json.Serialization;

namespace AIStudio.Assistants.SlideBuilder;

/// <summary>
/// Describes one title-and-content slide in a generated presentation.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SlideDeckPlanSlide
{
    /// <summary>Gets or sets the slide title.</summary>
    [JsonRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the slide's bullet points.</summary>
    [JsonRequired]
    public List<string> BulletPoints { get; set; } = [];
}
