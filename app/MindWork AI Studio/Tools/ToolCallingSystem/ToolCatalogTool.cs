namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// One tool of an entry in the tool catalog.
/// </summary>
/// <param name="Definition">What the tool is.</param>
/// <param name="Implementation">The implementation of the tool.</param>
/// <param name="ConfigurationState">Whether the settings of the tool are complete.</param>
public sealed record ToolCatalogTool(ToolDefinition Definition, IToolImplementation Implementation, ToolConfigurationState ConfigurationState);