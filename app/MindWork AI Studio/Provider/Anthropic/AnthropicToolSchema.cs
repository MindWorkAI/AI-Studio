using System.Text.Json;
using System.Text.Json.Nodes;

namespace AIStudio.Provider.Anthropic;

/// <summary>
/// Translates a tool's parameter schema into the form the Anthropic messages API accepts.
/// </summary>
/// <remarks>
/// The tool definitions express optional parameters the way OpenAI's strict mode requires it:
/// every property is listed in <c>required</c>, and an optional one is made nullable through
/// <c>"type": ["string", "null"]</c>, sometimes with <c>null</c> among its enum values.<br/><br/>
/// Anthropic states optionality the ordinary JSON Schema way — an optional parameter is simply
/// absent from <c>required</c> — and its validator rejects a <c>null</c> enum value outright:
/// <c>Invalid schema: Enum value None does not match declared type</c>. So the nullable-and-required
/// form has to be turned back into the plain one before the schema is sent.<br/><br/>
/// Nothing is lost in the translation. Both forms say "this parameter may be left out", and the
/// tools already treat an absent argument and a null one the same way.
/// </remarks>
public static class AnthropicToolSchema
{
    private const string NULL_TYPE = "null";

    /// <summary>
    /// Converts one parameter schema, leaving it untouched when it uses no nullable types.
    /// </summary>
    public static JsonElement FromToolParameters(JsonElement parameters)
    {
        if (parameters.ValueKind is not JsonValueKind.Object)
            return parameters;

        if (JsonNode.Parse(parameters.GetRawText()) is not JsonObject schema)
            return parameters;

        if (schema["properties"] is not JsonObject properties)
            return parameters;

        var optionalPropertyNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (propertyName, propertyNode) in properties)
        {
            if (propertyNode is not JsonObject property || !TryRemoveNullType(property))
                continue;

            optionalPropertyNames.Add(propertyName);
            RemoveNullEnumValues(property);
        }

        if (optionalPropertyNames.Count is 0)
            return parameters;

        //
        // A property that may be null is optional, so it leaves the required list. Whatever
        // remains keeps its order, which keeps the schema stable for prompt caching.
        //
        if (schema["required"] is JsonArray required)
        {
            var remainingRequired = required
                .Select(entry => entry?.GetValue<string>())
                .Where(entry => entry is not null && !optionalPropertyNames.Contains(entry))
                .ToList();

            schema["required"] = new JsonArray([..remainingRequired.Select(entry => JsonValue.Create(entry))]);
        }

        return JsonSerializer.Deserialize<JsonElement>(schema.ToJsonString());
    }

    /// <summary>
    /// Drops the null entry from a property's type, if it has one.
    /// </summary>
    /// <returns>True when the property declared a null type, which makes it an optional one.</returns>
    private static bool TryRemoveNullType(JsonObject property)
    {
        if (property["type"] is not JsonArray types)
            return false;

        var remainingTypes = types
            .Select(entry => entry?.GetValue<string>())
            .Where(entry => entry is not null && !entry.Equals(NULL_TYPE, StringComparison.Ordinal))
            .ToList();

        if (remainingTypes.Count == types.Count)
            return false;

        // A single remaining type is written as a plain string, which is the ordinary shape and
        // what a reader of the schema expects:
        property["type"] = remainingTypes.Count is 1
            ? JsonValue.Create(remainingTypes[0])
            : new JsonArray([..remainingTypes.Select(entry => JsonValue.Create(entry))]);

        return true;
    }

    private static void RemoveNullEnumValues(JsonObject property)
    {
        if (property["enum"] is not JsonArray enumValues)
            return;

        var remainingValues = enumValues
            .Where(entry => entry is not null)
            .Select(entry => entry!.DeepClone())
            .ToList();

        if (remainingValues.Count == enumValues.Count)
            return;

        property["enum"] = new JsonArray([..remainingValues]);
    }
}