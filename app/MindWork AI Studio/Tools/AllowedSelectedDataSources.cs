using AIStudio.Settings;
using AIStudio.Settings.DataModel;

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
/// searched, but Semantic Search and the classic RAG process both name them to the model, so that it
/// can tell the user what is missing instead of answering as though there were nothing.
/// </remarks>
/// <param name="AllowedDataSources">The allowed data sources.</param>
/// <param name="SelectedDataSources">The selected data sources, which are a subset of the allowed data sources.</param>
/// <param name="DataSourcesAwaitingReindex">The data sources which passed every check but cannot be searched until their index has been rebuilt.</param>
/// <param name="DataSourcesNeedingRepair">The data sources which passed every check but whose index cannot be read anymore, so that only the user can get them back.</param>
/// <param name="UnreachableDataSources">The ERI data sources whose server could not be reached or signed in to right now, and whose configured security policy allows them for this provider. Selected or not.</param>
public readonly record struct AllowedSelectedDataSources(IReadOnlyList<IDataSource> AllowedDataSources, IReadOnlyList<IDataSource> SelectedDataSources, IReadOnlyList<IDataSource> DataSourcesAwaitingReindex, IReadOnlyList<IDataSource> DataSourcesNeedingRepair, IReadOnlyList<IDataSource> UnreachableDataSources)
{
    /// <summary>
    /// The data sources whose server could not be reached, which the model learns about in this chat.
    /// </summary>
    /// <remarks>
    /// Only those the chat would have searched count: with the automatic selection all of them, since
    /// any data source may be picked, otherwise only those the user selected. A data source configured
    /// to return no matches is left out, because it would add nothing even when its server answers.
    /// Semantic Search and the classic RAG process both ask here, so that the model hears the same in
    /// either way.
    /// </remarks>
    /// <param name="options">The data source options of the chat.</param>
    /// <returns>The unreachable data sources of the chat, in the order they were checked.</returns>
    public IReadOnlyList<IDataSource> GetUnreachableOfTheChat(DataSourceOptions options) => this.UnreachableDataSources
        .Where(dataSource => dataSource.MaxMatches > 0)
        .Where(dataSource => options.AutomaticDataSourceSelection || options.PreselectedDataSourceIds.Contains(dataSource.Id))
        .ToList();
}