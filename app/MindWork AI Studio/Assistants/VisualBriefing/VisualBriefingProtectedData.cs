using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// The data of a briefing which AI Studio writes itself and no model may change, under the key <c>_mwai</c>.
/// </summary>
/// <param name="SchemaVersion">The version of the schema of the briefing data.</param>
/// <param name="RuntimeVersion">The version of the runtime script of the briefing.</param>
/// <param name="AIStudioVersion">The version of AI Studio which created the briefing.</param>
/// <param name="Assets">The embedded pictures, by their asset IDs.</param>
/// <param name="AssetMetadata">What the briefing tells about each picture, or null where the data only gets validated.</param>
/// <param name="Footer">The texts of the footer, by their keys.</param>
internal sealed record VisualBriefingProtectedData(
    int SchemaVersion,
    int RuntimeVersion,
    string AIStudioVersion,
    IReadOnlyDictionary<string, string> Assets,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Dictionary<string, VisualBriefingAssetMetadata>? AssetMetadata,
    Dictionary<string, string> Footer)
{
    private const string VALIDATION_VALUE = "validation";

    private const string VALIDATION_ASSET = "data:image/png;base64,AA==";

    /// <summary>
    /// Creates stand-in protected data for validating a compiled briefing before it gets assembled.
    /// </summary>
    /// <param name="assetPlan">The planned pictures of the briefing.</param>
    /// <returns>The stand-in protected data.</returns>
    internal static VisualBriefingProtectedData ForValidation(IEnumerable<VisualBriefingAssetPlanItem> assetPlan) => new(
        VisualBriefingVersions.SCHEMA,
        VisualBriefingVersions.RUNTIME,
        VALIDATION_VALUE,
        assetPlan.ToDictionary(asset => asset.AssetId, _ => VALIDATION_ASSET, StringComparer.Ordinal),
        null,
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["createdWith"] = VALIDATION_VALUE,
            ["models"] = VALIDATION_VALUE,
            ["createdAt"] = VALIDATION_VALUE,
            ["authors"] = VALIDATION_VALUE,
            ["protection"] = VALIDATION_VALUE,
        });
}