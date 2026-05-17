using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ERIClient.DataModel;
using AIStudio.Tools.PluginSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs.Settings;

public partial class SettingsDialogDataSources : SettingsDialogBase
{
    [Inject]
    private ISnackbar Snackbar { get; init; } = null!;

    private string GetEmbeddingName(IDataSource dataSource)
    {
        if(dataSource is IInternalDataSource internalDataSource)
        {
            var matchedEmbedding = this.SettingsManager.ConfigurationData.EmbeddingProviders.FirstOrDefault(x => x.Id == internalDataSource.EmbeddingId);
            if(matchedEmbedding == default)
                return T("No valid embedding");
            
            return matchedEmbedding.Name;
        }
        
        if(dataSource is IExternalDataSource)
            return T("External (ERI)");
        
        return T("Unknown");
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
                    { x => x.AvailableEmbeddings, this.AvailableEmbeddingProviders }
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
                    { x => x.AvailableEmbeddings, this.AvailableEmbeddingProviders }
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
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task ExportDataSource(IDataSource dataSource)
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
                await this.RustService.CopyText2Clipboard(this.Snackbar, publicLuaCode);

            return;
        }

        var secretResponse = await this.RustService.GetSecret(eriDataSource, SecretStoreType.DATA_SOURCE, isTrying: true);
        var canEncryptSecret = PluginFactory.EnterpriseEncryption?.IsAvailable == true;

        var dialogParameters = new DialogParameters<DataSourceERIV1ExportDialog>
        {
            { x => x.DataSource, eriDataSource },
            { x => x.HasConfiguredSecret, secretResponse.Success },
            { x => x.CanEncryptSecret, canEncryptSecret },
        };

        var dialogReference = await this.DialogService.ShowAsync<DataSourceERIV1ExportDialog>(T("Export ERI Data Source"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled || dialogResult.Data is not DataSourceERIV1ExportDialogResult exportResult)
            return;

        string? encryptedSecret = null;
        if (exportResult.IncludeSecret && secretResponse.Success)
        {
            var decryptedSecret = await secretResponse.Secret.Decrypt(Program.ENCRYPTION);
            var encryption = PluginFactory.EnterpriseEncryption;
            if (encryption?.TryEncrypt(decryptedSecret, out var encrypted) == true)
                encryptedSecret = encrypted;
            else
                this.Snackbar.Add(T("Cannot export the encrypted secret. A placeholder will be used instead."), Severity.Warning);
        }

        var luaCode = eriDataSource.ExportAsConfigurationSection(
            encryptedSecret,
            exportResult.UsernamePasswordMode,
            needsSecret && string.IsNullOrWhiteSpace(encryptedSecret));
        if (string.IsNullOrWhiteSpace(luaCode))
            return;

        await this.RustService.CopyText2Clipboard(this.Snackbar, luaCode);
    }
    
    private async Task EditDataSource(IDataSource dataSource)
    {
        if (dataSource.IsEnterpriseConfiguration)
            return;

        IDataSource? editedDataSource = null;
        switch (dataSource)
        {
            case DataSourceLocalFile localFile:
                var localFileDialogParameters = new DialogParameters<DataSourceLocalFileDialog>
                {
                    { x => x.IsEditing, true },
                    { x => x.DataSource, localFile },
                    { x => x.AvailableEmbeddings, this.AvailableEmbeddingProviders }
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
                    { x => x.AvailableEmbeddings, this.AvailableEmbeddingProviders }
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
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }
    
    private async Task DeleteDataSource(IDataSource dataSource)
    {
        if (dataSource.IsEnterpriseConfiguration)
            return;

        var dialogParameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.Message, string.Format(T("Are you sure you want to delete the data source '{0}' of type {1}?"), dataSource.Name, dataSource.Type.GetDisplayName()) },
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
            await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
        }
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