using AIStudio.Dialogs;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

/// <summary>
/// Adds and imports data sources and mailboxes. It is kept apart from DataSourceManagement, so that the
/// data source settings dialog can place these buttons in its footer, where they stay in reach however
/// long the table of data sources grows. The data source panel in the app settings places them below the table.
/// </summary>
public partial class DataSourceAddButtons : MSGComponentBase
{
    [Inject]
    private DataSourceEmbeddingService DataSourceEmbeddingService { get; init; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private RustService RustService { get; init; } = null!;

    private bool MayAddMailbox => this.SettingsManager.ConfigurationData.App.AllowUserToAddMailbox;

    /// <summary>
    /// Whether an embedding provider exists, which every local data source needs.
    /// </summary>
    private bool HasEmbeddingProvider => !EmbeddingProviderSetup.IsMissing(this.SettingsManager);

    /// <remarks>
    /// The rule of the organization comes first: an embedding provider would not make the mailbox possible.
    /// </remarks>
    private string MailboxMenuText => !this.MayAddMailbox
        ? T("Mailbox (IMAP), not allowed by your organization")
        : this.HasEmbeddingProvider
            ? T("Mailbox (IMAP)")
            : T("Mailbox (IMAP), requires an embedding provider");

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED ]);
    }

    #endregion

    #region Overrides of MSGComponentBase

    protected override Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            // The embedding providers and the rules of the organization decide what the buttons offer:
            case Event.CONFIGURATION_CHANGED:
                this.StateHasChanged();
                break;
        }

        return Task.CompletedTask;
    }

    #endregion

    private async Task ImportERIDataSource()
    {
        var import = await ConfigurationSnippetImportDialog.ShowAsync(this.DialogService, PluginConfigurationObjectType.DATA_SOURCE, T("Import Data Source"));
        if (import?.ConfigurationObject is not DataSourceERI_V1 dataSource)
            return;

        var eriDialogParameters = new DialogParameters<DataSourceERI_V1Dialog>
        {
            { x => x.IsEditing, false },
            { x => x.IsImport, true },
            { x => x.DataSource, dataSource },
            { x => x.ImportedSecret, import.Secret },
        };

        var eriDialogReference = await this.DialogService.ShowAsync<DataSourceERI_V1Dialog>(T("Add ERI v1 Data Source"), eriDialogParameters, DialogOptions.FULLSCREEN);
        var eriDialogResult = await eriDialogReference.Result;
        if (eriDialogResult is null || eriDialogResult.Canceled)
            return;

        //
        // An organization can lock the import while the user is still filling in the form, e.g.,
        // with a new version of its configuration plugin. Hence, the permission is checked once
        // more before the imported data source is stored:
        //
        if (!this.SettingsManager.ConfigurationData.App.CanImportConfigurationSnippet(PluginConfigurationObjectType.DATA_SOURCE))
            return;

        var eriDataSource = (DataSourceERI_V1)eriDialogResult.Data!;
        await this.StoreAddedDataSource(eriDataSource with { Num = this.SettingsManager.ConfigurationData.NextDataSourceNum++ });
    }

    private async Task AddDataSource(DataSourceType type)
    {
        // The menu offers no local data source without an embedding provider, but the last one may have gone since it was drawn:
        if (type is DataSourceType.LOCAL_FILE or DataSourceType.LOCAL_DIRECTORY && !this.HasEmbeddingProvider)
            return;

        IDataSource? addedDataSource = null;
        switch (type)
        {
            case DataSourceType.LOCAL_FILE:
                var localFileDialogParameters = new DialogParameters<DataSourceLocalFileDialog>
                {
                    { x => x.IsEditing, false },
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

        await this.StoreAddedDataSource(addedDataSource);
    }

    private async Task StoreAddedDataSource(IDataSource addedDataSource)
    {
        this.SettingsManager.ConfigurationData.DataSources.Add(addedDataSource);
        await this.SettingsManager.StoreSettings();
        await this.DataSourceEmbeddingService.QueueDataSourceAsync(addedDataSource);
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task AddMailbox()
    {
        if (!this.MayAddMailbox || !this.HasEmbeddingProvider)
            return;

        var dialogParameters = new DialogParameters<DataSourceMailboxDialog>
        {
            { x => x.IsEditing, false },
        };

        var dialogReference = await this.DialogService.ShowAsync<DataSourceMailboxDialog>(T("Add Mailbox"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var mailbox = (DataSourceMailbox)dialogResult.Data!;

        //
        // The dialog refuses to add a mailbox once the organization rules them out, but the rule
        // may change while it stores the password. Then the password goes again, as far as possible:
        //
        if (!this.MayAddMailbox)
        {
            await this.RustService.DeleteSecret(mailbox, SecretStoreType.DATA_SOURCE);
            return;
        }

        mailbox = mailbox with { Num = this.SettingsManager.ConfigurationData.NextDataSourceNum++ };

        this.SettingsManager.ConfigurationData.Mailboxes.Add(mailbox);
        await this.SettingsManager.StoreSettings();
        await this.DataSourceEmbeddingService.QueueDataSourceAsync(mailbox);
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }
}