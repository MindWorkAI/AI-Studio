using AIStudio.Settings;

namespace AIStudio.Tools;

/// <summary>
/// Contains both the allowed and selected data sources.
/// </summary>
/// <remarks>
/// The selected data sources are a subset of the allowed data sources.
/// </remarks>
/// <param name="AllowedDataSources">The allowed data sources.</param>
/// <param name="SelectedDataSources">The selected data sources, which are a subset of the allowed data sources.</param>
public readonly record struct AllowedSelectedDataSources(IReadOnlyList<IDataSource> AllowedDataSources, IReadOnlyList<IDataSource> SelectedDataSources);