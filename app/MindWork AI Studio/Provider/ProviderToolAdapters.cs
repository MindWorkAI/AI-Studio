using AIStudio.Provider.Anthropic;
using AIStudio.Provider.OpenAI;
using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Provider;

/// <summary>
/// Converts the canonical AI Studio tool definition into provider-specific wire shapes.
/// </summary>
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
            parameters = definition.Function.Parameters,
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
        Parameters = definition.Function.Parameters,
        Strict = definition.Function.Strict,
    };

    /// <summary>
    /// Builds the tool shape used by the Anthropic messages API.
    /// </summary>
    /// <remarks>
    /// Same schema, different field names: Anthropic calls the parameters an input schema and
    /// takes the description without nesting it under a function object.
    /// </remarks>
    public static AnthropicTool ToAnthropicTool(ToolDefinition definition) => new()
    {
        Name = definition.Function.Name,
        Description = definition.Function.DescriptionForLLM,
        InputSchema = definition.Function.Parameters,
        Strict = definition.Function.Strict,
    };
}
