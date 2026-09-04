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
/// the schema while Anthropic takes it as written.
/// </remarks>
public static class ProviderToolAdapters
{
    /// <summary>
    /// Builds the nested function tool shape used by Chat Completions compatible APIs.
    /// </summary>
    public static object ToChatCompletionTool(ToolDefinition definition) => new
    {
        type = "function",
        function = new
        {
            name = definition.Function.Name,
            description = definition.Function.DescriptionForLLM,
            parameters = ToOpenAIParameters(definition),
            strict = definition.Function.Strict,
        }
    };

    /// <summary>
    /// Builds the flat function tool shape used by the OpenAI Responses API.
    /// </summary>
    public static ResponsesFunctionTool ToResponsesTool(ToolDefinition definition) => new()
    {
        Name = definition.Function.Name,
        Description = definition.Function.DescriptionForLLM,
        Parameters = ToOpenAIParameters(definition),
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
    private static System.Text.Json.JsonElement ToOpenAIParameters(ToolDefinition definition) => definition.Function.Strict
        ? OpenAIStrictToolSchema.FromToolParameters(definition.Function.Parameters)
        : definition.Function.Parameters;
}
