using System.Text;

using AIStudio.Dialogs;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

namespace AIStudio.Tools;

/// <summary>
/// Asks before an edit makes the prepared documents of data sources useless, and names the data
/// sources which depend on an embedding provider somebody is about to delete.
/// </summary>
/// <remarks>
/// Kept here rather than in the dialogs which ask -- the embedding provider dialog and the two data
/// source dialogs -- so the sentence naming what a rebuild costs cannot drift apart between them.
/// That is the same reason DataSourceRepair sits next to it, and both name the same two costs.
///
/// Nothing is asked when nothing is lost. A data source only reaches the question when the edit
/// really changes its embedding signature and when the index already holds something for it, so
/// renaming an embedding provider or editing a data source nobody has indexed yet stays silent.
/// </remarks>
public static class DataSourceReindexWarning
{
    /// <summary>
    /// How many data sources are named before the rest is only counted.
    /// </summary>
    private const int MAX_NAMED_DATA_SOURCES = 10;

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(DataSourceReindexWarning).Namespace, nameof(DataSourceReindexWarning));

    /// <summary>
    /// Asks before an edited embedding provider is saved.
    /// </summary>
    /// <param name="dialogService">The dialog service to ask with.</param>
    /// <param name="settingsManager">The settings, read for the data sources behind the provider.</param>
    /// <param name="embeddingService">The service which knows what the index holds.</param>
    /// <param name="before">The embedding provider as it is stored.</param>
    /// <param name="after">The embedding provider as it would be stored.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>True when the edit may be saved.</returns>
    public static async Task<bool> ConfirmEmbeddingProviderChangeAsync(IDialogService dialogService, SettingsManager settingsManager, DataSourceEmbeddingService embeddingService,
        EmbeddingProvider before, EmbeddingProvider after, CancellationToken token = default)
    {
        // Nothing was stored under this id, so no data source can point at it:
        if (before == EmbeddingProvider.NONE)
            return true;

        var candidates = GetDataSourcesUsing(settingsManager, before.Id)
            .Where(dataSource => EmbeddingChangeImpact.AffectsStoredIndex(dataSource, before, after))
            .Cast<IDataSource>()
            .ToList();

        if (candidates.Count == 0)
            return true;

        var affected = await embeddingService.GetDataSourcesWithStoredIndexAsync(candidates, token);
        return await ConfirmAsync(dialogService, affected, !after.IsSelfHosted);
    }

    /// <summary>
    /// Asks before an edited data source is saved.
    /// </summary>
    /// <param name="dialogService">The dialog service to ask with.</param>
    /// <param name="settingsManager">The settings, read for the embedding provider of the data source.</param>
    /// <param name="embeddingService">The service which knows what the index holds.</param>
    /// <param name="before">The data source as it is stored.</param>
    /// <param name="after">The data source as it would be stored.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>True when the edit may be saved.</returns>
    public static async Task<bool> ConfirmDataSourceChangeAsync(IDialogService dialogService, SettingsManager settingsManager, DataSourceEmbeddingService embeddingService,
        IInternalDataSource before, IInternalDataSource after, CancellationToken token = default)
    {
        // Without a provider nothing is embedded at all, so nothing can be lost:
        if (!DataSourceEmbeddingProviders.TryResolve(settingsManager, after, out var embeddingProvider))
            return true;

        if (!EmbeddingChangeImpact.AffectsStoredIndex(embeddingProvider, before, after))
            return true;

        var affected = await embeddingService.GetDataSourcesWithStoredIndexAsync([after], token);
        return await ConfirmAsync(dialogService, affected, !embeddingProvider.IsSelfHosted);
    }

    /// <summary>
    /// Names the data sources which would lose their embedding provider, for the deletion question.
    /// </summary>
    /// <remarks>
    /// Deleting is the one case where nothing prepared is thrown away: the documents stay where they
    /// are, but nothing can reach them by meaning any more, and nothing new can be prepared either.
    /// The names come from the same place as the ones in the questions above so that both lists read
    /// alike, which is also why this returns the text instead of asking on its own -- the deletion
    /// question has more to say than this.
    ///
    /// Every data source pointing at the provider is named, prepared or not. A source which was never
    /// indexed loses just as much: it can no longer be prepared at all.
    /// </remarks>
    /// <param name="settingsManager">The settings holding the data sources.</param>
    /// <param name="embeddingProvider">The embedding provider which is about to be deleted.</param>
    /// <returns>The Markdown text, or an empty string when no data source uses that provider.</returns>
    public static string DescribeDataSourcesLosingTheirProvider(SettingsManager settingsManager, EmbeddingProvider embeddingProvider)
    {
        if (embeddingProvider == EmbeddingProvider.NONE)
            return string.Empty;

        var affected = GetDataSourcesUsing(settingsManager, embeddingProvider.Id).Cast<IDataSource>().ToList();
        if (affected.Count == 0)
            return string.Empty;

        var body = new StringBuilder();

        // Counted rather than put into a plural form: the I18N has no mechanism for one.
        body.AppendLine(string.Format(TB("These data sources are set up with this embedding provider ({0}):"), affected.Count.CompactCount()));
        body.AppendLine();
        body.AppendLine(FormatDataSourceNames(affected));
        body.AppendLine();
        body.AppendLine(TB("They keep answering keyword searches, but searching them by meaning stops working, and no further documents can be prepared for them. The ones which are already prepared stay tied to this provider as well, so you cannot simply move them to another one."));

        return body.ToString();
    }

    /// <summary>
    /// The data sources which are indexed with a given embedding provider.
    /// </summary>
    /// <param name="settingsManager">The settings holding the data sources.</param>
    /// <param name="embeddingProviderId">The id of the embedding provider.</param>
    /// <returns>The data sources pointing at that embedding provider.</returns>
    private static IReadOnlyList<IInternalDataSource> GetDataSourcesUsing(SettingsManager settingsManager, string embeddingProviderId) =>
        settingsManager.ConfigurationData.DataSources
            .OfType<IInternalDataSource>()
            .Where(dataSource => embeddingProviderId.Equals(dataSource.EmbeddingId, StringComparison.OrdinalIgnoreCase))
            .ToList();

    /// <summary>
    /// Names data sources as a Markdown list, counting the rest when there are too many to name.
    /// </summary>
    /// <param name="dataSources">The data sources to name.</param>
    /// <returns>The Markdown list.</returns>
    private static string FormatDataSourceNames(IReadOnlyList<IDataSource> dataSources)
    {
        var names = dataSources
            .Select(dataSource => dataSource.Name)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var lines = names.Take(MAX_NAMED_DATA_SOURCES).Select(name => $"- {name}").ToList();
        if (names.Count > MAX_NAMED_DATA_SOURCES)
            lines.Add($"- {string.Format(TB("and {0} more."), (names.Count - MAX_NAMED_DATA_SOURCES).CompactCount())}");

        return string.Join(Environment.NewLine, lines);
    }

    private static async Task<bool> ConfirmAsync(IDialogService dialogService, IReadOnlyList<IDataSource> affected, bool usesCloudEmbedding)
    {
        if (affected.Count == 0)
            return true;

        var body = new StringBuilder();

        // Counted rather than put into a plural form: the I18N has no mechanism for one.
        body.AppendLine(string.Format(TB("This change makes the prepared documents of the following data sources unusable ({0}):"), affected.Count.CompactCount()));
        body.AppendLine();
        body.AppendLine(FormatDataSourceNames(affected));
        body.AppendLine();
        body.AppendLine(TB("Everything prepared for them is thrown away, and every one of their documents goes to your embedding provider once more. With a large data source, this takes a while."));

        if (usesCloudEmbedding)
        {
            body.AppendLine();
            body.AppendLine(TB("Your embedding provider runs in the cloud, so preparing everything again costs money."));
        }

        body.AppendLine();
        body.AppendLine(TB("Do you want to apply this change anyway?"));

        var dialogParameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.MarkdownBody, body.ToString() },
        };

        var dialogReference = await dialogService.ShowAsync<ConfirmDialog>(TB("Documents Will Be Prepared Again"), dialogParameters, Dialogs.DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        return dialogResult is not null && !dialogResult.Canceled;
    }
}