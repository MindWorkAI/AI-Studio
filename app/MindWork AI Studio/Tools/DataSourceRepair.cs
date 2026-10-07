using AIStudio.Dialogs;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

namespace AIStudio.Tools;

/// <summary>
/// Asks whether a data source should be indexed anew, and starts the rebuild when the user agrees.
/// </summary>
/// <remarks>
/// Kept here rather than in the two places which offer the repair -- the background embeddings page
/// and the data source table -- so the sentence naming what a rebuild costs cannot drift apart
/// between them. Naming both costs is the whole reason for asking at all.
/// </remarks>
public static class DataSourceRepair
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(DataSourceRepair).Namespace, nameof(DataSourceRepair));

    /// <summary>
    /// Asks the user, and rebuilds the index of the data source when they agree.
    /// </summary>
    /// <param name="dialogService">The dialog service to ask with.</param>
    /// <param name="embeddingService">The service which does the rebuild.</param>
    /// <param name="dataSourceId">The data source to repair.</param>
    /// <param name="dataSourceName">The name of that data source, as the question names it.</param>
    /// <returns>True when the rebuild was started.</returns>
    public static async Task<bool> ConfirmAndRepairAsync(IDialogService dialogService, DataSourceEmbeddingService embeddingService, string dataSourceId, string dataSourceName)
    {
        var dialogParameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.Message, string.Format(TB("The index of the data source '{0}' cannot be read anymore. Repairing it means building the index from scratch: everything indexed so far is thrown away, and every document of this data source is sent to your embedding provider once more. With a cloud provider, this costs money, and with a large data source it takes a while. Do you want to repair this data source now?"), dataSourceName) },
        };

        var dialogReference = await dialogService.ShowAsync<ConfirmDialog>(TB("Repair Data Source"), dialogParameters, Dialogs.DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return false;

        await embeddingService.RepairDataSourceAsync(dataSourceId);
        return true;
    }
}