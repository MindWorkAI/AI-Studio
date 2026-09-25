using AIStudio.Provider;

namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// What a tool is: what the model may call, which settings it needs, and where it may be used.
/// </summary>
/// <remarks>
/// A record, so that the registry can hand out a definition whose function a tool tailored to one
/// request while everything else stays as registered, see IToolImplementation.ResolveFunctionAsync.
/// </remarks>
public sealed record ToolDefinition
{
    public int SchemaVersion { get; init; } = 1;

    public string Id { get; init; } = string.Empty;

    public string ImplementationKey { get; init; } = string.Empty;

    public ToolVisibilityDefinition VisibleIn { get; init; } = new();

    /// <summary>
    /// Whether the tool waits to be selected, or offers itself whenever the chat calls for it.
    /// </summary>
    public ToolActivation Activation { get; init; } = ToolActivation.SELECTION;

    public ToolSettingsSchema SettingsSchema { get; init; } = new();

    public string SystemPromptInstructions { get; init; } = string.Empty;

    /// <summary>
    /// The lowest provider confidence this tool may be used with, unless an administrator or the
    /// user says otherwise.
    /// </summary>
    /// <remarks>
    /// Belongs to the tool, because only the tool knows what it exposes: a web search sends the
    /// user's question to a search engine, so it asks for more trust than a calculator would.
    /// </remarks>
    public ConfidenceLevel MinimumProviderConfidence { get; init; } = ConfidenceLevel.NONE;

    public ToolFunctionDefinition Function { get; init; } = new();
}