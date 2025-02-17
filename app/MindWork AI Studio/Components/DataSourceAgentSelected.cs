using AIStudio.Agents;
using AIStudio.Settings;

namespace AIStudio.Components;

/// <summary>
/// A data structure to combine the data source and the underlying AI decision.
/// </summary>
public sealed class DataSourceAgentSelected
{
    /// <summary>
    /// The data source.
    /// </summary>
    public required IDataSource DataSource { get; set; }

    /// <summary>
    /// The AI decision, which led to the selection of the data source.
    /// </summary>
    public required SelectedDataSource AIDecision { get; set; }

    /// <summary>
    /// Indicates whether the data source is part of the final selection for the RAG process.
    /// </summary>
    public bool Selected { get; set; }
}