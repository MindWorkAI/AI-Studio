using System.Text.Json;

namespace AIStudio.Provider;

/// <summary>
/// Parses the provider-specific JSON fragment stored in <see cref="IProvider.AdditionalJsonApiParameters"/>.
/// </summary>
/// <remarks>
/// The provider settings UI stores only the body of a JSON object, such as
/// <c>"temperature": 0.5</c>. This parser wraps that fragment in curly braces,
/// parses it as JSON, and converts it to regular CLR dictionaries, lists, and
/// primitive values so request builders and feature detectors can inspect it.
/// </remarks>
public static class AdditionalApiParametersParser
{
    /// <summary>
    /// Try to parse an additional-API-parameters JSON fragment into a dictionary.
    /// </summary>
    /// <param name="additionalJsonApiParameters">The JSON object body without the surrounding curly braces.</param>
    /// <param name="parameters">The parsed parameters if parsing succeeds; otherwise an empty dictionary.</param>
    /// <param name="errorMessage">The JSON parsing error message if parsing fails; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the fragment is empty or valid JSON; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string additionalJsonApiParameters, out IDictionary<string, object> parameters, out string? errorMessage)
    {
        parameters = new Dictionary<string, object>();
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(additionalJsonApiParameters))
            return true;

        try
        {
            // The UI stores only the object body, so wrap it before parsing.
            using var jsonDoc = JsonDocument.Parse($"{{{additionalJsonApiParameters}}}");
            parameters = ConvertToDictionary(jsonDoc.RootElement);
            return true;
        }
        catch (JsonException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Remove keys from a parsed parameter dictionary using case-insensitive matching.
    /// </summary>
    /// <param name="parameters">The parsed parameter dictionary to mutate.</param>
    /// <param name="keysToRemove">The parameter names that should be removed.</param>
    /// <returns>The same dictionary instance after the matching keys were removed.</returns>
    public static IDictionary<string, object> RemoveKeys(IDictionary<string, object> parameters, IEnumerable<string> keysToRemove)
    {
        var removeSet = new HashSet<string>(keysToRemove, StringComparer.OrdinalIgnoreCase);
        if (removeSet.Count is 0)
            return parameters;

        foreach (var key in parameters.Keys.ToList())
            if (removeSet.Contains(key))
                parameters.Remove(key);

        return parameters;
    }

    /// <summary>
    /// Convert a JSON object element into a dictionary of recursively converted CLR values.
    /// </summary>
    /// <param name="element">The JSON object element to convert.</param>
    /// <returns>A dictionary containing all JSON object properties.</returns>
    private static IDictionary<string, object> ConvertToDictionary(JsonElement element)
    {
        return element.EnumerateObject()
            .ToDictionary<JsonProperty, string, object>(
                p => p.Name,
                p => ConvertJsonValue(p.Value) ?? string.Empty
            );
    }

    /// <summary>
    /// Convert a JSON value to the closest CLR representation used by provider request objects.
    /// </summary>
    /// <param name="element">The JSON element to convert.</param>
    /// <returns>A string, number, boolean, dictionary, list, or empty string for unsupported/null values.</returns>
    private static object? ConvertJsonValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt32(out var i) ? i :
            element.TryGetInt64(out var l) ? l :
            element.TryGetDouble(out var d) ? d :
            element.GetDecimal(),
        JsonValueKind.True or JsonValueKind.False => element.GetBoolean(),
        JsonValueKind.Null => string.Empty,
        JsonValueKind.Object => ConvertToDictionary(element),
        JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonValue).ToList(),

        _ => string.Empty,
    };
}