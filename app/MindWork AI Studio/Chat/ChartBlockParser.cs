using System.Text;
using System.Text.Json;

namespace AIStudio.Chat;

/// <summary>
/// Parses and validates versioned AI Studio chart blocks without executing their content.
/// </summary>
public static class ChartBlockParser
{
    private const int MAX_JSON_BYTES = 32 * 1024;
    private const int MAX_CATEGORIES = 50;
    private const int MAX_SERIES = 10;

    private static readonly HashSet<string> ROOT_PROPERTIES = ["schema_version", "type", "title", "caption", "data"];
    private static readonly HashSet<string> DATA_PROPERTIES = ["categories", "series"];
    private static readonly HashSet<string> SERIES_PROPERTIES = ["name", "values"];

    /// <summary>
    /// Parses a JSON chart definition and applies the complete local version 1 validation contract.
    /// </summary>
    public static ChartBlockParseResult Parse(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MAX_JSON_BYTES)
            return ChartBlockParseResult.Invalid(json, "The chart JSON exceeds the 32 KB limit.");

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8,
            });

            var root = document.RootElement;
            if (root.ValueKind is not JsonValueKind.Object)
                return ChartBlockParseResult.Invalid(json, "The chart definition must be a JSON object.");

            if (!HasOnlyKnownProperties(root, ROOT_PROPERTIES, out var propertyError))
                return ChartBlockParseResult.Invalid(json, propertyError);

            if (!TryGetRequiredInt(root, "schema_version", out var schemaVersion) || schemaVersion != 1)
                return ChartBlockParseResult.Invalid(json, "schema_version must be 1.");

            if (!TryGetRequiredString(root, "type", out var typeText)
                || !TryParseType(typeText, out var type))
                return ChartBlockParseResult.Invalid(json, "type must be bar, stacked_bar, line, pie, or donut.");

            if (!TryGetRequiredString(root, "title", out var title) || string.IsNullOrWhiteSpace(title))
                return ChartBlockParseResult.Invalid(json, "title must be a non-empty string.");

            string? caption = null;
            if (root.TryGetProperty("caption", out var captionElement))
            {
                if (captionElement.ValueKind is not JsonValueKind.String
                    || string.IsNullOrWhiteSpace(captionElement.GetString()))
                    return ChartBlockParseResult.Invalid(json, "caption must be a non-empty string when provided.");

                caption = captionElement.GetString();
            }

            if (!root.TryGetProperty("data", out var data) || data.ValueKind is not JsonValueKind.Object)
                return ChartBlockParseResult.Invalid(json, "data must be a JSON object.");

            if (!HasOnlyKnownProperties(data, DATA_PROPERTIES, out propertyError))
                return ChartBlockParseResult.Invalid(json, propertyError);

            if (!TryReadCategories(data, out var categories, out var error))
                return ChartBlockParseResult.Invalid(json, error);

            if (!TryReadSeries(data, categories.Count, out var series, out error))
                return ChartBlockParseResult.Invalid(json, error);

            if (type is ChartDefinitionType.PIE or ChartDefinitionType.DONUT)
            {
                if (series.Count != 1)
                    return ChartBlockParseResult.Invalid(json, "Pie and donut charts require exactly one series.");

                if (series[0].Values.Any(value => value < 0))
                    return ChartBlockParseResult.Invalid(json, "Pie and donut chart values must not be negative.");
            }

            return ChartBlockParseResult.Valid(json, new(schemaVersion, type, title, caption, categories, series));
        }
        catch (JsonException exception)
        {
            return ChartBlockParseResult.Invalid(json, $"The chart JSON is invalid: {exception.Message}");
        }
    }

    private static bool TryReadCategories(JsonElement data, out IReadOnlyList<string> categories, out string error)
    {
        categories = [];
        error = string.Empty;
        if (!data.TryGetProperty("categories", out var element) || element.ValueKind is not JsonValueKind.Array)
        {
            error = "data.categories must be an array.";
            return false;
        }

        var values = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind is not JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                error = "Every category must be a non-empty string.";
                return false;
            }

            values.Add(item.GetString()!);
            if (values.Count > MAX_CATEGORIES)
            {
                error = $"A chart can contain at most {MAX_CATEGORIES} categories.";
                return false;
            }
        }

        if (values.Count == 0)
        {
            error = "A chart requires at least one category.";
            return false;
        }

        categories = values;
        return true;
    }

    private static bool TryReadSeries(JsonElement data, int categoryCount, out IReadOnlyList<ChartDefinitionSeries> series, out string error)
    {
        series = [];
        error = string.Empty;
        if (!data.TryGetProperty("series", out var element) || element.ValueKind is not JsonValueKind.Array)
        {
            error = "data.series must be an array.";
            return false;
        }

        var result = new List<ChartDefinitionSeries>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind is not JsonValueKind.Object)
            {
                error = "Every series must be a JSON object.";
                return false;
            }

            if (!HasOnlyKnownProperties(item, SERIES_PROPERTIES, out error))
                return false;

            if (!TryGetRequiredString(item, "name", out var name) || string.IsNullOrWhiteSpace(name))
            {
                error = "Every series requires a non-empty name.";
                return false;
            }

            if (!item.TryGetProperty("values", out var valuesElement) || valuesElement.ValueKind is not JsonValueKind.Array)
            {
                error = "Every series requires a values array.";
                return false;
            }

            var values = new List<double>();
            foreach (var valueElement in valuesElement.EnumerateArray())
            {
                if (valueElement.ValueKind is not JsonValueKind.Number
                    || !valueElement.TryGetDouble(out var value)
                    || !double.IsFinite(value))
                {
                    error = "Series values must be finite JSON numbers.";
                    return false;
                }

                values.Add(value);
            }

            if (values.Count != categoryCount)
            {
                error = "Every series must contain exactly one value per category.";
                return false;
            }

            result.Add(new(name, values));
            if (result.Count > MAX_SERIES)
            {
                error = $"A chart can contain at most {MAX_SERIES} series.";
                return false;
            }
        }

        if (result.Count == 0)
        {
            error = "A chart requires at least one series.";
            return false;
        }

        series = result;
        return true;
    }

    private static bool HasOnlyKnownProperties(JsonElement element, HashSet<string> knownProperties, out string error)
    {
        var seenProperties = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!knownProperties.Contains(property.Name))
            {
                error = $"Unknown chart property: {property.Name}.";
                return false;
            }

            if (!seenProperties.Add(property.Name))
            {
                error = $"Duplicate chart property: {property.Name}.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool TryGetRequiredInt(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        return element.TryGetProperty(propertyName, out var property)
               && property.ValueKind is JsonValueKind.Number
               && property.TryGetInt32(out value);
    }

    private static bool TryGetRequiredString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is not JsonValueKind.String)
            return false;

        value = property.GetString() ?? string.Empty;
        return true;
    }

    private static bool TryParseType(string value, out ChartDefinitionType type)
    {
        type = value switch
        {
            "bar" => ChartDefinitionType.BAR,
            "stacked_bar" => ChartDefinitionType.STACKED_BAR,
            "line" => ChartDefinitionType.LINE,
            "pie" => ChartDefinitionType.PIE,
            "donut" => ChartDefinitionType.DONUT,
            _ => default,
        };

        return value is "bar" or "stacked_bar" or "line" or "pie" or "donut";
    }
}

/// <summary>
/// The safe result of parsing a chart block, including the original JSON for fallback display.
/// </summary>
public sealed record ChartBlockParseResult(string RawJson, ChartDefinition? Chart, string Error)
{
    public bool IsValid => this.Chart is not null;

    public static ChartBlockParseResult Valid(string rawJson, ChartDefinition chart) => new(rawJson, chart, string.Empty);

    public static ChartBlockParseResult Invalid(string rawJson, string error) => new(rawJson, null, error);
}
