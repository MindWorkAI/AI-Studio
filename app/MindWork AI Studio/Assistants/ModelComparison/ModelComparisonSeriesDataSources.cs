using AIStudio.Settings.DataModel;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// The data sources both chats of a comparison series may search, and how they search them.
/// </summary>
/// <param name="DataSourceIds">The IDs of the data sources.</param>
/// <param name="RetrievalMode">How both chats search them, or null without any data source.</param>
public readonly record struct ModelComparisonSeriesDataSources(IReadOnlyList<string> DataSourceIds, DataSourceRetrievalMode? RetrievalMode)
{
    /// <summary>
    /// No data source at all.
    /// </summary>
    public static readonly ModelComparisonSeriesDataSources NONE = new([], null);
}