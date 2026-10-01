using System.Text.Json;

namespace AIStudio.Provider.OpenAI;

/// <summary>
/// The flat function tool definition shape expected by the OpenAI Responses API.
/// </summary>
public sealed record ResponsesFunctionTool
{
    public string Type { get; init; } = "function";

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public JsonElement Parameters { get; init; }

    public bool Strict { get; init; }
}
