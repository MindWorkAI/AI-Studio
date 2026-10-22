using AIStudio.Dialogs;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Tools;

/// <summary>
/// Opens the settings of a data source, and stores what the user changed in them.
/// </summary>
/// <remarks>
/// Kept here rather than in the places which offer it -- the data source table and the background
/// embeddings page, which leads to it when the file or the folder of a data source cannot be
/// reached -- so that saving a data source works the same way everywhere: the settings are stored,
/// and the data source is indexed right after. Mailboxes are kept in a list of their own and have
/// MailboxEditing instead.
/// </remarks>
public static class DataSourceEditing
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(DataSourceEditing).Namespace, nameof(DataSourceEditing));

    /// <summary>
    /// Whether the user may edit a data source.
    /// </summary>
    /// <param name="settingsManager">The settings, which hold the data source.</param>
    /// <param name="dataSourceId">The id of the data source.</param>
    /// <returns>False when the data source is gone or managed by the organization.</returns>
    public static bool CanEdit(SettingsManager settingsManager, string dataSourceId) =>
        settingsManager.ConfigurationData.DataSources.Any(dataSource => dataSource.Id == dataSourceId && !dataSource.IsEnterpriseConfiguration);

    /// <summary>
    /// Lets the user edit a data source, and stores and indexes it when they save.
    /// </summary>
    /// <remarks>
    /// The caller tells the rest of the app that the configuration changed, as the sender of that message.
    /// </remarks>
    /// <param name="dialogService">The dialog service to show the settings with.</param>
    /// <param name="settingsManager">The settings, which hold the data source.</param>
    /// <param name="embeddingService">The service which indexes the data source.</param>
    /// <param name="dataSourceId">The id of the data source.</param>
    /// <returns>True when the user saved the data source. False when they cancelled, or the data source is gone or managed by the organization.</returns>
    public static async Task<bool> EditAsync(IDialogService dialogService, SettingsManager settingsManager, DataSourceEmbeddingService embeddingService, string dataSourceId)
    {
        var dataSources = settingsManager.ConfigurationData.DataSources;
        var storedDataSource = dataSources.Find(dataSource => dataSource.Id == dataSourceId);
        if (storedDataSource is null || storedDataSource.IsEnterpriseConfiguration)
            return false;

        IDataSource? editedDataSource = null;
        switch (storedDataSource)
        {
            case DataSourceLocalFile localFile:
                var localFileDialogParameters = new DialogParameters<DataSourceLocalFileDialog>
                {
                    { x => x.IsEditing, true },
                    { x => x.DataSource, localFile },
                };

                var localFileDialogReference = await dialogService.ShowAsync<DataSourceLocalFileDialog>(TB("Edit Local File Data Source"), localFileDialogParameters, DialogOptions.FULLSCREEN);
                var localFileDialogResult = await localFileDialogReference.Result;
                if (localFileDialogResult is null || localFileDialogResult.Canceled)
                    return false;

                editedDataSource = (DataSourceLocalFile)localFileDialogResult.Data!;
                break;

            case DataSourceLocalDirectory localDirectory:
                var localDirectoryDialogParameters = new DialogParameters<DataSourceLocalDirectoryDialog>
                {
                    { x => x.IsEditing, true },
                    { x => x.DataSource, localDirectory },
                };

                var localDirectoryDialogReference = await dialogService.ShowAsync<DataSourceLocalDirectoryDialog>(TB("Edit Local Directory Data Source"), localDirectoryDialogParameters, DialogOptions.FULLSCREEN);
                var localDirectoryDialogResult = await localDirectoryDialogReference.Result;
                if (localDirectoryDialogResult is null || localDirectoryDialogResult.Canceled)
                    return false;

                editedDataSource = (DataSourceLocalDirectory)localDirectoryDialogResult.Data!;
                break;

            case DataSourceERI_V1 eriDataSource:
                var eriDialogParameters = new DialogParameters<DataSourceERI_V1Dialog>
                {
                    { x => x.IsEditing, true },
                    { x => x.DataSource, eriDataSource },
                };

                var eriDialogReference = await dialogService.ShowAsync<DataSourceERI_V1Dialog>(TB("Edit ERI v1 Data Source"), eriDialogParameters, DialogOptions.FULLSCREEN);
                var eriDialogResult = await eriDialogReference.Result;
                if (eriDialogResult is null || eriDialogResult.Canceled)
                    return false;

                editedDataSource = (DataSourceERI_V1)eriDialogResult.Data!;
                break;
        }

        if (editedDataSource is null)
            return false;

        // Found anew, since the list may have changed while the dialog was open:
        var position = dataSources.IndexOf(storedDataSource);
        if (position < 0)
            return false;

        dataSources[position] = editedDataSource;

        await settingsManager.StoreSettings();
        await embeddingService.QueueDataSourceAsync(editedDataSource);
        return true;
    }
}