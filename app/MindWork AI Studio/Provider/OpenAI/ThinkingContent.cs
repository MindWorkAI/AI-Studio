using System.Text.Json;

namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Reads human-readable thinking from OpenAI-compatible response fields.
/// </summary>
internal static class ThinkingContent
{
    public static string Get(string? reasoningContent, string? reasoning, IEnumerable<JsonElement>? reasoningDetails)
    {
        if (!string.IsNullOrWhiteSpace(reasoningContent))
            return reasoningContent;

        if (!string.IsNullOrWhiteSpace(reasoning))
            return reasoning;

        if (reasoningDetails is null)
            return string.Empty;

        return string.Concat(reasoningDetails.Select(GetVisibleDetailText));
    }

    private static string GetVisibleDetailText(JsonElement detail)
    {
        if (detail.ValueKind is not JsonValueKind.Object ||
            !detail.TryGetProperty("type", out var typeProperty) ||
            typeProperty.ValueKind is not JsonValueKind.String)
            return string.Empty;

        return typeProperty.GetString() switch
        {
            "reasoning.text" => ReadString(detail, "text"),
            "reasoning.summary" => ReadString(detail, "summary"),
            _ => string.Empty,
        };
    }

    private static string ReadString(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var property) || property.ValueKind is not JsonValueKind.String)
            return string.Empty;

        return property.GetString() ?? string.Empty;
    }
}
