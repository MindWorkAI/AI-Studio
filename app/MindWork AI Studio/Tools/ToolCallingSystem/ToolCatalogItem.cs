using AIStudio.Provider;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolCatalogItem
{
    public required ToolDefinition Definition { get; init; }

    public required IToolImplementation Implementation { get; init; }

    public required ToolConfigurationState ConfigurationState { get; init; }

    public bool IsActive { get; init; }

    public ConfidenceLevel MinimumProviderConfidence { get; init; } = ConfidenceLevel.NONE;
}