using System.Text.Json;
using System.Text.Json.Nodes;

namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Translates a tool's parameter schema into the form OpenAI's strict mode requires.
/// </summary>
/// <remarks>
/// Strict mode does not accept an optional argument the ordinary JSON Schema way. It insists that
/// every property appears in <c>required</c>, and an argument that may be left out has to say so
/// by allowing null instead — <c>"type": ["string", "null"]</c>, and <c>null</c> among its enum
/// values where it has any.<br/><br/>
/// Tool definitions are written the ordinary way, so this converts on the way out. Both forms mean
/// the same to a tool: a null argument and an absent one are treated alike.
/// </remarks>
public static class OpenAIStrictToolSchema
{
    private const string NULL_TYPE = "null";

    /// <summary>
    /// Converts one parameter schema, leaving it untouched when every argument is required
    /// anyway.
    /// </summary>
    public static JsonElement FromToolParameters(JsonElement parameters)
    {
        if (parameters.ValueKind is not JsonValueKind.Object)
            return parameters;

        if (JsonNode.Parse(parameters.GetRawText()) is not JsonObject schema)
            return parameters;

        if (schema["properties"] is not JsonObject properties)
            return parameters;

        var requiredNames = schema["required"] is JsonArray required
            ? required.Select(entry => entry?.GetValue<string>()).Where(entry => entry is not null).ToHashSet(StringComparer.Ordinal)
            : [];

        var optionalPropertyNames = properties
            .Select(property => property.Key)
            .Where(propertyName => !requiredNames.Contains(propertyName))
            .ToList();

        if (optionalPropertyNames.Count is 0)
            return parameters;

        foreach (var propertyName in optionalPropertyNames)
        {
            if (properties[propertyName] is not JsonObject property)
                continue;

            AllowNullType(property);
            AllowNullEnumValue(property);
        }

        //
        // Every property is required in strict mode. The order follows the properties, so the
        // schema stays stable across requests, which prompt caching depends on.
        //
        schema["required"] = new JsonArray([..properties.Select(property => JsonValue.Create(property.Key))]);
        return JsonSerializer.Deserialize<JsonElement>(schema.ToJsonString());
    }

    private static void AllowNullType(JsonObject property)
    {
        switch (property["type"])
        {
            case JsonValue singleType when singleType.TryGetValue<string>(out var typeName) && !typeName.Equals(NULL_TYPE, StringComparison.Ordinal):
                property["type"] = new JsonArray(JsonValue.Create(typeName), JsonValue.Create(NULL_TYPE));
                break;

            case JsonArray types when types.All(entry => entry?.GetValue<string>() != NULL_TYPE):
                types.Add(JsonValue.Create(NULL_TYPE));
                break;
        }
    }

    private static void AllowNullEnumValue(JsonObject property)
    {
        // Only where the property restricts its values at all: adding null to an absent enum
        // would turn an unrestricted argument into one that may only be null.
        if (property["enum"] is not JsonArray enumValues || enumValues.Any(entry => entry is null))
            return;

        enumValues.Insert(0, null);
    }
}