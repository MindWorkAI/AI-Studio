using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Describes one visual asset without embedding its bytes.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[CanonicalJsonShape("d05cdc87")]
public sealed class VisualBriefingAssetPlanItem
{
    /// <summary>
    /// Gets or sets the stable visual asset identifier.
    /// </summary>
    [JsonRequired]
    public string AssetId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the model's visual description for presentation decisions.
    /// </summary>
    [JsonRequired]
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the target-language text alternative.
    /// </summary>
    [JsonRequired]
    public string AltText { get; init; } = string.Empty;
}