using AIStudio.Provider;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolDefinition
{
    public int SchemaVersion { get; init; } = 1;

    public string Id { get; init; } = string.Empty;

    public string ImplementationKey { get; init; } = string.Empty;

    public ToolVisibilityDefinition VisibleIn { get; init; } = new();

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