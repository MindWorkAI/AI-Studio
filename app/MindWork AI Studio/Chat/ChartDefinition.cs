namespace AIStudio.Chat;

/// <summary>
/// A renderer-independent, validated chart definition produced by an AI response.
/// </summary>
public sealed record ChartDefinition(
    int SchemaVersion,
    ChartDefinitionType Type,
    string Title,
    string? Caption,
    IReadOnlyList<string> Categories,
    IReadOnlyList<ChartDefinitionSeries> Series);

/// <summary>
/// A named series in a chart definition.
/// </summary>
public sealed record ChartDefinitionSeries(string Name, IReadOnlyList<double> Values);

/// <summary>
/// Chart types supported by version 1 of the AI Studio chart contract.
/// </summary>
public enum ChartDefinitionType
{
    BAR,
    STACKED_BAR,
    LINE,
    PIE,
    DONUT,
    HEATMAP,
    TIME_SERIES,
}
