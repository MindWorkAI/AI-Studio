using AIStudio.Settings;

namespace AIStudio.Tools;

/// <summary>
/// Contains the allowed and selected data sources, plus the ones which cannot be searched right now.
/// </summary>
/// <remarks>
/// The selected data sources are a subset of the allowed data sources.
///
/// The data sources waiting for a re-index are deliberately kept apart from the allowed ones rather
/// than mixed in. Everything reading the allowed list -- the data source selection agent above all
/// -- takes it to mean "may be used to answer with", and a source whose index is being rebuilt
/// cannot answer anything. It is listed separately so the user interface can still show it and say
/// why it is greyed out, instead of letting it vanish without a word.
///
/// The same holds for the ones waiting for a repair, and they are a list of their own because the
/// two reasons call for different words: one passes by itself, the other one waits for the user.
/// A data source is in at most one of the two lists.
///
/// The ERI data sources whose server could not be reached are a list of their own, too: they are
/// neither allowed nor refused, because their server's requirements are unknown. They must never be
/// searched, but Semantic Search names them to the model, so that it can tell the user what is
/// missing instead of answering as though there were nothing.
/// </remarks>
/// <param name="AllowedDataSources">The allowed data sources.</param>
/// <param name="SelectedDataSources">The selected data sources, which are a subset of the allowed data sources.</param>
/// <param name="DataSourcesAwaitingReindex">The data sources which passed every check but cannot be searched until their index has been rebuilt.</param>
/// <param name="DataSourcesNeedingRepair">The data sources which passed every check but whose index cannot be read anymore, so that only the user can get them back.</param>
/// <param name="UnreachableDataSources">The ERI data sources whose server could not be reached right now, and whose configured security policy allows them for this provider. Selected or not.</param>
public readonly record struct AllowedSelectedDataSources(IReadOnlyList<IDataSource> AllowedDataSources, IReadOnlyList<IDataSource> SelectedDataSources, IReadOnlyList<IDataSource> DataSourcesAwaitingReindex, IReadOnlyList<IDataSource> DataSourcesNeedingRepair, IReadOnlyList<IDataSource> UnreachableDataSources);