using System.Text.Json;

namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// The function a tool offers the model: its name, what it does, and the arguments it takes.
/// </summary>
/// <remarks>
/// A record, so that a tool tailoring its function to a request changes only what it has to, e.g.
/// definition.Function with { DescriptionForLLM = … }, see IToolImplementation.ResolveFunctionAsync.
/// </remarks>
public sealed record ToolFunctionDefinition
{
    public string Name { get; init; } = string.Empty;

    public string DescriptionForLLM { get; init; } = string.Empty;

    /// <summary>
    /// Whether this tool may be offered in strict mode.
    /// </summary>
    /// <remarks>
    /// The host has a say as well: a tool goes strict only where the host binds the model's calls
    /// to the schema, see ProviderToolAdapters. Setting this to false keeps a tool out of strict
    /// mode everywhere, for a schema which strict mode cannot express.
    /// </remarks>
    public bool Strict { get; init; } = true;

    public JsonElement Parameters { get; init; }
}