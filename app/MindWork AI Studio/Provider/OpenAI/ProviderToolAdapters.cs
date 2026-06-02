using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Provider.OpenAI;

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
            description = definition.Function.Description,
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
        Description = definition.Function.Description,
        Parameters = definition.Function.Parameters,
        Strict = definition.Function.Strict,
    };
}
