using AIStudio.Settings;

namespace AIStudio.Tools;

/// <summary>
/// Contains the allowed and selected data sources, plus the ones waiting for their index.
/// </summary>
/// <remarks>
/// The selected data sources are a subset of the allowed data sources.
///
/// The data sources waiting for a re-index are deliberately kept apart from the allowed ones rather
/// than mixed in. Everything reading the allowed list -- the data source selection agent above all
/// -- takes it to mean "may be used to answer with", and a source whose index is being rebuilt
/// cannot answer anything. It is listed separately so the user interface can still show it and say
/// why it is greyed out, instead of letting it vanish without a word.
/// </remarks>
/// <param name="AllowedDataSources">The allowed data sources.</param>
/// <param name="SelectedDataSources">The selected data sources, which are a subset of the allowed data sources.</param>
/// <param name="DataSourcesAwaitingReindex">The data sources which passed every check but cannot be searched until their index has been rebuilt.</param>
public readonly record struct AllowedSelectedDataSources(IReadOnlyList<IDataSource> AllowedDataSources, IReadOnlyList<IDataSource> SelectedDataSources, IReadOnlyList<IDataSource> DataSourcesAwaitingReindex);