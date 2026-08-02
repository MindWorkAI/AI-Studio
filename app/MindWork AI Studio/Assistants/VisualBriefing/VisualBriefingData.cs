using System.Text.Json;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Centralizes protected-data and embedded-asset transformations.
/// </summary>
internal static class VisualBriefingData
{
    /// <summary>
    /// Removes the app-owned protected block from artifact data.
    /// </summary>
    /// <param name="data">Artifact data.</param>
    /// <returns>Canonical business data.</returns>
    internal static JsonElement RemoveProtectedData(JsonElement data)
    {
        var dictionary = data.EnumerateObject()
            .Where(property => property.Name is not "_mwai")
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);
        return JsonSerializer.SerializeToElement(dictionary, VisualBriefingJson.Canonical);
    }

    /// <summary>
    /// Extracts the single protected embedded-asset map.
    /// </summary>
    /// <param name="data">Artifact data.</param>
    /// <returns>Stable asset IDs mapped to Data URLs.</returns>
    internal static Dictionary<string, string> ExtractAssets(JsonElement data)
    {
        if (!data.TryGetProperty("_mwai", out var protectedData) ||
            protectedData.ValueKind is not JsonValueKind.Object ||
            !protectedData.TryGetProperty("assets", out var assets) ||
            assets.ValueKind is not JsonValueKind.Object)
            return [];

        return assets.EnumerateObject()
            .Where(property => property.Value.ValueKind is JsonValueKind.String)
            .ToDictionary(
                property => property.Name,
                property => property.Value.GetString() ?? string.Empty,
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Extracts protected visual asset descriptions and text alternatives.
    /// </summary>
    /// <param name="data">Artifact data.</param>
    /// <returns>The extracted asset plan.</returns>
    internal static List<VisualBriefingAssetPlanItem> ExtractAssetPlan(JsonElement data)
    {
        if (!data.TryGetProperty("_mwai", out var protectedData) ||
            protectedData.ValueKind is not JsonValueKind.Object ||
            !protectedData.TryGetProperty("assetMetadata", out var metadata) ||
            metadata.ValueKind is not JsonValueKind.Object)
            return [];

        List<VisualBriefingAssetPlanItem> result = [];
        foreach (var property in metadata.EnumerateObject())
        {
            if (property.Value.ValueKind is not JsonValueKind.Object ||
                !property.Value.TryGetProperty("description", out var description) ||
                description.ValueKind is not JsonValueKind.String ||
                !property.Value.TryGetProperty("altText", out var altText) ||
                altText.ValueKind is not JsonValueKind.String)
                continue;
            result.Add(new()
            {
                AssetId = property.Name,
                Description = description.GetString() ?? string.Empty,
                AltText = altText.GetString() ?? string.Empty,
            });
        }
        return result;
    }

    /// <summary>
    /// Rejects Data URLs and the protected namespace in model-owned business data.
    /// </summary>
    /// <param name="data">The model-owned data.</param>
    /// <returns>An empty string on success or a safe validation issue.</returns>
    internal static string ValidateBusinessData(JsonElement data)
    {
        if (data.ValueKind is not JsonValueKind.Object)
            return "The canonical content data must be one JSON object.";
        if (data.TryGetProperty("_mwai", out _))
            return "The canonical content data uses the reserved _mwai property.";
        if (ContainsDataUrl(data))
            return "The canonical content data must reference assets by stable ID and cannot contain Data URLs.";
        return string.Empty;
    }

    /// <summary>
    /// Detects embedded Data URLs recursively.
    /// </summary>
    /// <param name="value">The JSON value to inspect.</param>
    /// <returns>Whether a Data URL is present.</returns>
    private static bool ContainsDataUrl(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Array => value.EnumerateArray().Any(ContainsDataUrl),
        JsonValueKind.Object => value.EnumerateObject().Any(property => ContainsDataUrl(property.Value)),
        JsonValueKind.String => value.GetString()?.StartsWith("data:", StringComparison.OrdinalIgnoreCase) == true,
        _ => false,
    };
}
