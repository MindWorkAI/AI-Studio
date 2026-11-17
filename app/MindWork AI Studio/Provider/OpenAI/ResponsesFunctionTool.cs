using System.Text.Json;

namespace AIStudio.Provider.OpenAI;

/// <summary>
/// The flat function tool definition shape expected by the OpenAI Responses API.
/// </summary>
public sealed record ResponsesFunctionTool
{
    public string Type { get; set; } = "function";

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public JsonElement Parameters { get; set; }

    public bool Strict { get; set; }
}
