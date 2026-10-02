using AIStudio.Dialogs;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases;
using AIStudio.Tools.ERIClient.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

/// <summary>
/// Manages the configured data sources. Used by the data source settings dialog, which the chat
/// opens, and by the data source panel in the app settings.
/// </summary>
public partial class DataSourceManagement : MSGComponentBase
{
    [Inject]
    private DataSourceEmbeddingService DataSourceEmbeddingService { get; init; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private RustService RustService { get; init; } = null!;

    [Inject]
    private DatabaseClientProvider DatabaseClientProvider { get; init; } = null!;

    private readonly List<ConfigurationSelectData<string>> availableEmbeddingProviders = new();

    /// <summary>
    /// The data sources and then the mailboxes, which the table shows together.
    /// </summary>
    private IEnumerable<IDataSourceBase> ConfiguredDataSources => this.SettingsManager.ConfigurationData.DataSources
        .Concat(this.SettingsManager.ConfigurationData.Mailboxes.Cast<IDataSourceBase>());

    private bool AreMailboxesEnabled => PreviewFeatures.PRE_MAILBOXES_2026.IsEnabled(this.SettingsManager);

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED, Event.RAG_EMBEDDING_STATUS_CHANGED ]);
        this.UpdateEmbeddingProviders();
    }

    #endregion

    #region Overrides of MSGComponentBase

    protected override Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.CONFIGURATION_CHANGED:
            case Event.PLUGINS_RELOADED:
                this.UpdateEmbeddingProviders();
                this.StateHasChanged();
                break;

            case Event.RAG_EMBEDDING_STATUS_CHANGED:
                this.StateHasChanged();
                break;
        }

        return Task.CompletedTask;
    }

    #endregion

    private void UpdateEmbeddingProviders()
    {
        this.availableEmbeddingProviders.Clear();
        foreach (var provider in this.SettingsManager.GetAllEmbeddingProviders())
            this.availableEmbeddingProviders.Add(new (provider.Name, provider.Id));
    }

    /// <remarks>
    /// Files which were skipped for good are none of the failed ones, so a data source made of
    /// scanned documents stays green: there is nothing here for the user to fix.
    /// </remarks>
    private static Color GetIndexingStatusColor(DataSourceEmbeddingStatus? status)
    {
        if (status is null || status.State is DataSourceEmbeddingState.IDLE or DataSourceEmbeddingState.QUEUED or DataSourceEmbeddingState.RUNNING)
            return Color.Warning;

        return status.State is DataSourceEmbeddingState.FAILED || status.FailedDocuments > 0
            ? Color.Error
            : Color.Success;
    }

    /// <summary>
    /// Explains the indexing dot, including the files which stay out of the index.
    /// </summary>
    /// <remarks>
    /// The column shows the indexed files against the total, which reads as unfinished for a data
    /// source whose remaining files were skipped for good. The tooltip is where that gap gets its
    /// explanation.
    /// </remarks>
    private string GetIndexingStatusTooltip(DataSourceEmbeddingStatus? status)
    {
        if (status is null)
            return T("Waiting for indexing status");

        // Only files are skipped for want of readable text. A mail is read from the server, never from a file:
        if (status.PermanentlySkippedDocuments == 0 || status.DataSourceType is DataSourceType.MAILBOX)
            return status.StateLabel;

        return $"{status.StateLabel} — {string.Format(T("{0} files were skipped because they contain no readable text. AI Studio reads them again once they change."), status.PermanentlySkippedDocuments)}";
    }

    private string GetEmbeddingName(IDataSourceBase dataSource)
    {
        if(dataSource is IIndexedDataSource indexedDataSource)
        {
            var matchedEmbedding = this.SettingsManager.ConfigurationData.EmbeddingProviders.FirstOrDefault(x => x.Id == indexedDataSource.EmbeddingId);
            if(matchedEmbedding == default)
                return T("No valid embedding");

            return matchedEmbedding.Name;
        }

        if(dataSource is IExternalDataSource)
            return T("External (ERI)");

        return T("Unknown");
    }

    private bool CanRefreshDataSource(IDataSourceBase dataSource)
    {
        return this.DataSourceEmbeddingService.CanRefreshDataSource(dataSource);
    }

    private bool HasRefreshableDataSources()
    {
        return this.ConfiguredDataSources.Any(this.CanRefreshDataSource);
    }

    /// <remarks>
    /// Shown only while the index of this data source cannot be read. The refresh button next to it
    /// stays as it is: it would open the same store and fail the same way, but it is offered for
    /// every internal data source regardless of state, and singling this one out would say more
    /// about the state than that button ever has.
    /// </remarks>
    private bool CanRepairDataSource(IDataSourceBase dataSource)
    {
        return this.DataSourceEmbeddingService.NeedsIndexRepair(dataSource);
    }

    private async Task RepairDataSource(IDataSourceBase dataSource)
    {
        if (!this.CanRepairDataSource(dataSource))
            return;

        await DataSourceRepair.ConfirmAndRepairAsync(this.DialogService, this.DataSourceEmbeddingService, dataSource.Id, dataSource.Name);
    }

    private async Task AutomaticRefreshChanged(bool enabled)
    {
        this.SettingsManager.ConfigurationData.App.DataSourceIndexing.AutomaticRefresh = enabled;
        await this.SettingsManager.StoreSettings();
        this.DataSourceEmbeddingService.RefreshAutomaticWatchers();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task RefreshAllDataSources()
    {
        await this.DataSourceEmbeddingService.QueueAllInternalDataSourcesAsync();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task RefreshDataSource(IDataSourceBase dataSource)
    {
        if (!this.CanRefreshDataSource(dataSource))
            return;

        await this.DataSourceEmbeddingService.QueueDataSourceAsync(dataSource);
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task AddDataSource(DataSourceType type)
    {
        IDataSource? addedDataSource = null;
        switch (type)
        {
            case DataSourceType.LOCAL_FILE:
                var localFileDialogParameters = new DialogParameters<DataSourceLocalFileDialog>
                {
                    { x => x.IsEditing, false },
                    { x => x.AvailableEmbeddings, this.availableEmbeddingProviders }
                };

                var localFileDialogReference = await this.DialogService.ShowAsync<DataSourceLocalFileDialog>(T("Add Local File as Data Source"), localFileDialogParameters, DialogOptions.FULLSCREEN);
                var localFileDialogResult = await localFileDialogReference.Result;
                if (localFileDialogResult is null || localFileDialogResult.Canceled)
                    return;

                var localFile = (DataSourceLocalFile)localFileDialogResult.Data!;
                localFile = localFile with { Num = this.SettingsManager.ConfigurationData.NextDataSourceNum++ };
                addedDataSource = localFile;
                break;

            case DataSourceType.LOCAL_DIRECTORY:
                var localDirectoryDialogParameters = new DialogParameters<DataSourceLocalDirectoryDialog>
                {
                    { x => x.IsEditing, false },
                    { x => x.AvailableEmbeddings, this.availableEmbeddingProviders }
                };

                var localDirectoryDialogReference = await this.DialogService.ShowAsync<DataSourceLocalDirectoryDialog>(T("Add Local Directory as Data Source"), localDirectoryDialogParameters, DialogOptions.FULLSCREEN);
                var localDirectoryDialogResult = await localDirectoryDialogReference.Result;
                if (localDirectoryDialogResult is null || localDirectoryDialogResult.Canceled)
                    return;

                var localDirectory = (DataSourceLocalDirectory)localDirectoryDialogResult.Data!;
                localDirectory = localDirectory with { Num = this.SettingsManager.ConfigurationData.NextDataSourceNum++ };
                addedDataSource = localDirectory;
                break;

            case DataSourceType.ERI_V1:
                var eriDialogParameters = new DialogParameters<DataSourceERI_V1Dialog>
                {
                    { x => x.IsEditing, false },
                };

                var eriDialogReference = await this.DialogService.ShowAsync<DataSourceERI_V1Dialog>(T("Add ERI v1 Data Source"), eriDialogParameters, DialogOptions.FULLSCREEN);
                var eriDialogResult = await eriDialogReference.Result;
                if (eriDialogResult is null || eriDialogResult.Canceled)
                    return;

                var eriDataSource = (DataSourceERI_V1)eriDialogResult.Data!;
                eriDataSource = eriDataSource with { Num = this.SettingsManager.ConfigurationData.NextDataSourceNum++ };
                addedDataSource = eriDataSource;
                break;
        }

        if(addedDataSource is null)
            return;

        this.SettingsManager.ConfigurationData.DataSources.Add(addedDataSource);
        await this.SettingsManager.StoreSettings();
        await this.DataSourceEmbeddingService.QueueDataSourceAsync(addedDataSource);
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task ExportDataSource(IDataSourceBase dataSource)
    {
        if (!this.SettingsManager.ConfigurationData.App.ShowAdminSettings)
            return;

        if (dataSource is not DataSourceERI_V1 eriDataSource)
            return;

        if (eriDataSource.AuthMethod is AuthMethod.KERBEROS)
        {
            await this.DialogService.ShowMessageBox(
                T("Export ERI Data Source"),
                T("Kerberos/SSO ERI data sources cannot be exported yet. Please configure them manually in the configuration plugin."),
                T("Close"));
            return;
        }

        var needsSecret = eriDataSource.AuthMethod is AuthMethod.TOKEN or AuthMethod.USERNAME_PASSWORD;
        if (!needsSecret)
        {
            var publicLuaCode = eriDataSource.ExportAsConfigurationSection();
            if (!string.IsNullOrWhiteSpace(publicLuaCode))
                await this.RustService.CopyText2Clipboard(publicLuaCode);

            return;
        }

        var secretResponse = await this.RustService.GetSecret(eriDataSource, SecretStoreType.DATA_SOURCE, isTrying: true);
        if (!secretResponse.Success)
        {
            await this.DialogService.ShowMessageBox(
                T("Export ERI Data Source"),
                string.Format(T("Cannot export this ERI data source because no authentication secret is configured. The issue was: {0}"), secretResponse.Issue),
                T("Close"));
            return;
        }

        var encryption = PluginFactory.EnterpriseEncryption;
        if (encryption?.IsAvailable != true)
        {
            await this.DialogService.ShowMessageBox(
                T("Export ERI Data Source"),
                T("Cannot export this ERI data source because no enterprise encryption secret is configured."),
                T("Close"));
            return;
        }

        var usernamePasswordMode = DataSourceERIUsernamePasswordMode.USER_MANAGED;
        if (eriDataSource.AuthMethod is AuthMethod.TOKEN)
        {
            var dialogParameters = new DialogParameters<ConfirmDialog>
            {
                { x => x.Message, T("This ERI data source has an access token configured. Do you want to include the encrypted access token in the export? Note: The recipient will need the same encryption secret to use the access token.") },
            };

            var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Export Access Token?"), dialogParameters, DialogOptions.FULLSCREEN);
            var dialogResult = await dialogReference.Result;
            if (dialogResult is null || dialogResult.Canceled)
                return;
        }
        else if (eriDataSource.AuthMethod is AuthMethod.USERNAME_PASSWORD)
        {
            var dialogParameters = new DialogParameters<DataSourceERIV1UsernamePasswordExportDialog>
            {
                { x => x.DataSource, eriDataSource },
            };

            var dialogReference = await this.DialogService.ShowAsync<DataSourceERIV1UsernamePasswordExportDialog>(T("Export ERI Data Source"), dialogParameters, DialogOptions.FULLSCREEN);
            var dialogResult = await dialogReference.Result;
            if (dialogResult is null || dialogResult.Canceled || dialogResult.Data is not DataSourceERIV1UsernamePasswordExportDialogResult exportResult)
                return;

            usernamePasswordMode = exportResult.UsernamePasswordMode;
        }

        var decryptedSecret = await secretResponse.Secret.Decrypt(Program.ENCRYPTION);
        if (!encryption.TryEncrypt(decryptedSecret, out var encryptedSecret))
        {
            await this.DialogService.ShowMessageBox(
                T("Export ERI Data Source"),
                T("Cannot export this ERI data source because the authentication secret could not be encrypted."),
                T("Close"));
            return;
        }

        var luaCode = eriDataSource.ExportAsConfigurationSection(
            encryptedSecret,
            usernamePasswordMode);
        if (string.IsNullOrWhiteSpace(luaCode))
            return;

        await this.RustService.CopyText2Clipboard(luaCode);
    }

    private async Task EditDataSource(IDataSource dataSource)
    {
        if (dataSource.IsEnterpriseConfiguration)
            return;

        IDataSource? editedDataSource = null;
        var lockDataSourceOrigin = dataSource is IInternalDataSource
            && await this.DataSourceEmbeddingService.ShouldLockDataSourceOriginAsync(dataSource.Id);
        switch (dataSource)
        {
            case DataSourceLocalFile localFile:
                var localFileDialogParameters = new DialogParameters<DataSourceLocalFileDialog>
                {
                    { x => x.IsEditing, true },
                    { x => x.DataSource, localFile },
                    { x => x.LockSource, lockDataSourceOrigin },
                    { x => x.AvailableEmbeddings, this.availableEmbeddingProviders }
                };

                var localFileDialogReference = await this.DialogService.ShowAsync<DataSourceLocalFileDialog>(T("Edit Local File Data Source"), localFileDialogParameters, DialogOptions.FULLSCREEN);
                var localFileDialogResult = await localFileDialogReference.Result;
                if (localFileDialogResult is null || localFileDialogResult.Canceled)
                    return;

                editedDataSource = (DataSourceLocalFile)localFileDialogResult.Data!;
                break;

            case DataSourceLocalDirectory localDirectory:
                var localDirectoryDialogParameters = new DialogParameters<DataSourceLocalDirectoryDialog>
                {
                    { x => x.IsEditing, true },
                    { x => x.DataSource, localDirectory },
                    { x => x.LockSource, lockDataSourceOrigin },
                    { x => x.AvailableEmbeddings, this.availableEmbeddingProviders }
                };

                var localDirectoryDialogReference = await this.DialogService.ShowAsync<DataSourceLocalDirectoryDialog>(T("Edit Local Directory Data Source"), localDirectoryDialogParameters, DialogOptions.FULLSCREEN);
                var localDirectoryDialogResult = await localDirectoryDialogReference.Result;
                if (localDirectoryDialogResult is null || localDirectoryDialogResult.Canceled)
                    return;

                editedDataSource = (DataSourceLocalDirectory)localDirectoryDialogResult.Data!;
                break;

            case DataSourceERI_V1 eriDataSource:
                var eriDialogParameters = new DialogParameters<DataSourceERI_V1Dialog>
                {
                    { x => x.IsEditing, true },
                    { x => x.DataSource, eriDataSource },
                };

                var eriDialogReference = await this.DialogService.ShowAsync<DataSourceERI_V1Dialog>(T("Edit ERI v1 Data Source"), eriDialogParameters, DialogOptions.FULLSCREEN);
                var eriDialogResult = await eriDialogReference.Result;
                if (eriDialogResult is null || eriDialogResult.Canceled)
                    return;

                editedDataSource = (DataSourceERI_V1)eriDialogResult.Data!;
                break;
        }

        if(editedDataSource is null)
            return;

        this.SettingsManager.ConfigurationData.DataSources[this.SettingsManager.ConfigurationData.DataSources.IndexOf(dataSource)] = editedDataSource;

        await this.SettingsManager.StoreSettings();
        await this.DataSourceEmbeddingService.QueueDataSourceAsync(editedDataSource);
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task DeleteDataSource(IDataSource dataSource)
    {
        if (dataSource.IsEnterpriseConfiguration)
            return;

        var dialogParameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.Message, string.Format(T("Are you sure you want to delete the data source '{0}' of type '{1}'?"), dataSource.Name, dataSource.Type.GetDisplayName()) },
        };

        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete Data Source"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var applyChanges = dataSource is IInternalDataSource;

        // External data sources may need a secret for authentication:
        if (dataSource is IExternalDataSource externalDataSource)
        {
            // When the auth method is NONE or KERBEROS, we don't need to delete a secret.
            // In the case of KERBEROS, we don't store the Kerberos ticket in the secret store.
            if(dataSource is IERIDataSource { AuthMethod: AuthMethod.NONE or AuthMethod.KERBEROS })
                applyChanges = true;

            // All other auth methods require a secret, which we need to delete now:
            else
            {
                var deleteSecretResponse = await this.RustService.DeleteSecret(externalDataSource, SecretStoreType.DATA_SOURCE);
                if (deleteSecretResponse.Success)
                    applyChanges = true;
            }
        }

        if(applyChanges)
        {
            this.SettingsManager.ConfigurationData.DataSources.Remove(dataSource);
            await this.SettingsManager.StoreSettings();
            await this.DataSourceEmbeddingService.RemoveDataSourceAsync(dataSource);
            await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
        }
    }

    /// <summary>
    /// Edits a row of the table, which is either a data source or a mailbox.
    /// </summary>
    private Task EditEntry(IDataSourceBase entry) => entry switch
    {
        DataSourceMailbox mailbox => this.EditMailbox(mailbox),
        IDataSource dataSource => this.EditDataSource(dataSource),

        _ => Task.CompletedTask,
    };

    /// <summary>
    /// Deletes a row of the table, which is either a data source or a mailbox.
    /// </summary>
    private Task DeleteEntry(IDataSourceBase entry) => entry switch
    {
        DataSourceMailbox mailbox => this.DeleteMailbox(mailbox),
        IDataSource dataSource => this.DeleteDataSource(dataSource),

        _ => Task.CompletedTask,
    };

    private async Task AddMailbox()
    {
        var dialogParameters = new DialogParameters<DataSourceMailboxDialog>
        {
            { x => x.IsEditing, false },
            { x => x.AvailableEmbeddings, this.availableEmbeddingProviders }
        };

        var dialogReference = await this.DialogService.ShowAsync<DataSourceMailboxDialog>(T("Add Mailbox"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var mailbox = (DataSourceMailbox)dialogResult.Data!;
        mailbox = mailbox with { Num = this.SettingsManager.ConfigurationData.NextDataSourceNum++ };

        this.SettingsManager.ConfigurationData.Mailboxes.Add(mailbox);
        await this.SettingsManager.StoreSettings();
        await this.DataSourceEmbeddingService.QueueDataSourceAsync(mailbox);
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task EditMailbox(DataSourceMailbox mailbox)
    {
        if (mailbox.IsEnterpriseConfiguration)
            return;

        var dialogParameters = new DialogParameters<DataSourceMailboxDialog>
        {
            { x => x.IsEditing, true },
            { x => x.DataSource, mailbox },
            { x => x.LockSource, await this.DataSourceEmbeddingService.ShouldLockDataSourceOriginAsync(mailbox.Id) },
            { x => x.AvailableEmbeddings, this.availableEmbeddingProviders }
        };

        var dialogReference = await this.DialogService.ShowAsync<DataSourceMailboxDialog>(T("Edit Mailbox"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var editedMailbox = (DataSourceMailbox)dialogResult.Data!;
        var mailboxes = this.SettingsManager.ConfigurationData.Mailboxes;
        mailboxes[mailboxes.IndexOf(mailbox)] = editedMailbox;

        await this.SettingsManager.StoreSettings();
        await this.DataSourceEmbeddingService.QueueDataSourceAsync(editedMailbox);
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    /// <summary>
    /// Deletes a mailbox from AI Studio: its settings, its password, its index, and its recorded sign-in failure.
    /// </summary>
    /// <remarks>
    /// The sign-in failure is deleted on its own, since it outlives the index on purpose: repairing
    /// an index must not give the server a new attempt with a password it already refused.
    /// </remarks>
    private async Task DeleteMailbox(DataSourceMailbox mailbox)
    {
        if (mailbox.IsEnterpriseConfiguration)
            return;

        var dialogParameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.Message, string.Format(T("Are you sure you want to delete the mailbox '{0}'? Your mails stay on the server as they are. AI Studio only deletes its index of them and the stored password."), mailbox.Name) },
        };

        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete Mailbox"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var deleteSecretResponse = await this.RustService.DeleteSecret(mailbox, SecretStoreType.DATA_SOURCE);
        if (!deleteSecretResponse.Success)
        {
            await this.DialogService.ShowMessageBox(
                T("Delete Mailbox"),
                string.Format(T("The password of this mailbox could not be deleted from the operating system, so the mailbox was kept. The issue was: {0}"), deleteSecretResponse.Issue),
                T("Close"));
            return;
        }

        this.SettingsManager.ConfigurationData.Mailboxes.Remove(mailbox);
        await this.SettingsManager.StoreSettings();
        await this.DataSourceEmbeddingService.RemoveDataSourceAsync(mailbox);

        var indexStore = await this.DatabaseClientProvider.GetIndexStoreAsync();
        await indexStore.ClearMailboxAuthFailureAsync(mailbox.Id, CancellationToken.None);

        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task ShowInformation(IDataSource dataSource)
    {
        switch (dataSource)
        {
            case DataSourceLocalFile localFile:
                var localFileDialogParameters = new DialogParameters<DataSourceLocalFileInfoDialog>
                {
                    { x => x.DataSource, localFile },
                };

                await this.DialogService.ShowAsync<DataSourceLocalFileInfoDialog>(T("Local File Data Source Information"), localFileDialogParameters, DialogOptions.FULLSCREEN);
                break;

            case DataSourceLocalDirectory localDirectory:
                var localDirectoryDialogParameters = new DialogParameters<DataSourceLocalDirectoryInfoDialog>
                {
                    { x => x.DataSource, localDirectory },
                };

                await this.DialogService.ShowAsync<DataSourceLocalDirectoryInfoDialog>(T("Local Directory Data Source Information"), localDirectoryDialogParameters, DialogOptions.FULLSCREEN);
                break;

            case DataSourceERI_V1 eriV1DataSource:
                var eriV1DialogParameters = new DialogParameters<DataSourceERI_V1InfoDialog>
                {
                    { x => x.DataSource, eriV1DataSource },
                };

                await this.DialogService.ShowAsync<DataSourceERI_V1InfoDialog>(T("ERI v1 Data Source Information"), eriV1DialogParameters, DialogOptions.FULLSCREEN);
                break;
        }
    }
}
