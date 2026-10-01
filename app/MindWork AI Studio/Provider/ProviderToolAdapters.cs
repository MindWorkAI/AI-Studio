using AIStudio.Provider.Anthropic;
using AIStudio.Provider.OpenAI;
using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Provider;

/// <summary>
/// Converts a tool definition into the wire shape one provider API expects.
/// </summary>
/// <remarks>
/// The definitions state a tool once, in plain JSON Schema. What differs per API is not only the
/// field names but how an optional argument is expressed, which is why the OpenAI shapes convert
/// the schema while Anthropic takes it as written.<br/><br/>
/// Strict mode is a promise of the host, not of the definition: only a host which binds the
/// model's output to the schema keeps it. Everywhere else the model merely reads a schema that
/// calls every argument required, and then either invents a value for an argument it meant to
/// leave out, or the host rejects the call for lacking one. That is why the Chat Completions
/// shape, which reaches every OpenAI-compatible host, only goes strict where the host says so.
/// </remarks>
public static class ProviderToolAdapters
{
    /// <summary>
    /// Builds the nested function tool shape used by Chat Completions compatible APIs.
    /// </summary>
    /// <param name="definition">The tool to describe.</param>
    /// <param name="hostEnforcesStrict">Whether the host binds the model's tool calls to a strict schema. Only then is the tool sent in strict mode.</param>
    public static object ToChatCompletionTool(ToolDefinition definition, bool hostEnforcesStrict)
    {
        var isStrict = definition.Function.Strict && hostEnforcesStrict;
        return new
        {
            type = "function",
            function = new
            {
                name = definition.Function.Name,
                description = definition.Function.DescriptionForLLM,
                parameters = ToOpenAIParameters(definition, isStrict),
                strict = isStrict,
            }
        };
    }

    /// <summary>
    /// Builds the flat function tool shape used by the OpenAI Responses API.
    /// </summary>
    /// <remarks>
    /// Only OpenAI speaks this API, and it enforces strict mode, so the definition alone decides.
    /// </remarks>
    public static ResponsesFunctionTool ToResponsesTool(ToolDefinition definition) => new()
    {
        Name = definition.Function.Name,
        Description = definition.Function.DescriptionForLLM,
        Parameters = ToOpenAIParameters(definition, definition.Function.Strict),
        Strict = definition.Function.Strict,
    };

    /// <summary>
    /// Builds the tool shape used by the Anthropic messages API.
    /// </summary>
    /// <remarks>
    /// Different field names — Anthropic calls the parameters an input schema and takes the
    /// description without nesting it under a function object — but the schema itself needs no
    /// conversion: Anthropic reads optionality the same way the definitions write it.
    /// </remarks>
    public static AnthropicTool ToAnthropicTool(ToolDefinition definition) => new()
    {
        Name = definition.Function.Name,
        Description = definition.Function.DescriptionForLLM,
        InputSchema = definition.Function.Parameters,
        Strict = definition.Function.Strict,
    };

    /// <summary>
    /// The parameter schema for the OpenAI APIs, converted only when strict mode asks for it.
    /// </summary>
    private static System.Text.Json.JsonElement ToOpenAIParameters(ToolDefinition definition, bool isStrict) => isStrict
        ? OpenAIStrictToolSchema.FromToolParameters(definition.Function.Parameters)
        : definition.Function.Parameters;
}
