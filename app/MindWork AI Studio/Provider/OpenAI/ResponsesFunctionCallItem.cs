namespace AIStudio.Provider.OpenAI;

/// <summary>
/// A function call item returned by the OpenAI Responses API.
/// </summary>
public sealed record ResponsesFunctionCallItem
{
    public string? Type { get; init; }

    public string? CallId { get; init; }

    public string? Name { get; init; }

    public string? Arguments { get; init; }
}
