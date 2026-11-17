namespace AIStudio.Provider.OpenAI;

/// <summary>
/// A local function result item sent back to the OpenAI Responses API.
/// </summary>
public sealed record ResponsesFunctionCallOutputItem
{
    public string Type { get; set; } = "function_call_output";

    public string CallId { get; set; } = string.Empty;

    public string Output { get; set; } = string.Empty;
}
