namespace AIStudio.Provider.OpenAI;

/// <summary>
/// A function call item returned by the OpenAI Responses API.
/// </summary>
public sealed record ResponsesFunctionCallItem
{
    public string Type { get; init; } = string.Empty;

    public string CallId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Arguments { get; init; } = string.Empty;
}
