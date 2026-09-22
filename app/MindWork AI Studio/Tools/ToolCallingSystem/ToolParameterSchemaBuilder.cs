using System.Text.Json;
using System.Text.Json.Nodes;

namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// Builds the JSON Schema describing a tool's arguments.
/// </summary>
/// <remarks>
/// The schema is written the ordinary JSON Schema way: an optional argument is simply absent
/// from the required list. Providers whose APIs want it differently get it converted in their
/// adapter — OpenAI's strict mode, for instance, wants every argument required and the optional
/// ones nullable instead.<br/><br/>
/// Argument names come in as constants that the reading code shares, so the schema and the code
/// pulling the values apart cannot drift.
/// </remarks>
public sealed class ToolParameterSchemaBuilder
{
    private readonly JsonObject properties = new();
    private readonly List<string> requiredNames = [];

    public static ToolParameterSchemaBuilder Create() => new();

    public ToolParameterSchemaBuilder RequiredString(string name, string description) => this.Add(name, "string", description, isRequired: true);

    public ToolParameterSchemaBuilder OptionalString(string name, string description) => this.Add(name, "string", description, isRequired: false);

    public ToolParameterSchemaBuilder RequiredInteger(string name, string description) => this.Add(name, "integer", description, isRequired: true);

    public ToolParameterSchemaBuilder OptionalInteger(string name, string description) => this.Add(name, "integer", description, isRequired: false);

    public ToolParameterSchemaBuilder RequiredEnum(string name, string description, params string[] allowedValues) => this.Add(name, "string", description, isRequired: true, allowedValues);

    public ToolParameterSchemaBuilder OptionalEnum(string name, string description, params string[] allowedValues) => this.Add(name, "string", description, isRequired: false, allowedValues);

    /// <summary>
    /// Produces the finished schema.
    /// </summary>
    /// <remarks>
    /// Additional properties are refused: an argument AI Studio does not know about is a
    /// misunderstanding, not something to pass on to a tool.
    /// </remarks>
    public JsonElement Build()
    {
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = this.properties.DeepClone(),
            ["required"] = new JsonArray([..this.requiredNames.Select(name => JsonValue.Create(name))]),
            ["additionalProperties"] = false,
        };

        return JsonSerializer.Deserialize<JsonElement>(schema.ToJsonString());
    }

    private ToolParameterSchemaBuilder Add(string name, string jsonType, string description, bool isRequired, IReadOnlyList<string>? allowedValues = null)
    {
        var property = new JsonObject
        {
            ["type"] = jsonType,
            ["description"] = description,
        };

        if (allowedValues is { Count: > 0 })
            property["enum"] = new JsonArray([..allowedValues.Select(value => JsonValue.Create(value))]);

        this.properties[name] = property;
        if (isRequired)
            this.requiredNames.Add(name);

        return this;
    }
}