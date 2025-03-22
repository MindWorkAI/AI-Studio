using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ERIClient.DataModel;

namespace AIStudio.Dialogs.Settings;

public partial class SettingsDialogDataSources : SettingsDialogBase
{
    private string GetEmbeddingName(IDataSource dataSource)
    {
        if(dataSource is IInternalDataSource internalDataSource)
        {
            var matchedEmbedding = this.SettingsManager.ConfigurationData.EmbeddingProviders.FirstOrDefault(x => x.Id == internalDataSource.EmbeddingId);
            if(matchedEmbedding == default)
                return "No valid embedding";
            
            return matchedEmbedding.Name;
        }
        
        if(dataSource is IExternalDataSource)
            return "External (ERI)";
        
        return "Unknown";
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
        
                var localFileDialogReference = await this.DialogService.ShowAsync<DataSourceLocalFileDialog>("Add Local File as Data Source", localFileDialogParameters, DialogOptions.FULLSCREEN);
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
        
                var localDirectoryDialogReference = await this.DialogService.ShowAsync<DataSourceLocalDirectoryDialog>("Add Local Directory as Data Source", localDirectoryDialogParameters, DialogOptions.FULLSCREEN);
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
                
                var eriDialogReference = await this.DialogService.ShowAsync<DataSourceERI_V1Dialog>("Add ERI v1 Data Source", eriDialogParameters, DialogOptions.FULLSCREEN);
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
    
    private async Task EditDataSource(IDataSource dataSource)
    {
        IDataSource? editedDataSource = null;
        switch (dataSource)
        {
            case DataSourceLocalFile localFile:
                var localFileDialogParameters = new DialogParameters<DataSourceLocalFileDialog>
                {
                    { x => x.IsEditing, true },
                    { x => x.DataSource, localFile },
                    { x => x.AvailableEmbeddings, this.availableEmbeddingProviders }
                };
        
                var localFileDialogReference = await this.DialogService.ShowAsync<DataSourceLocalFileDialog>("Edit Local File Data Source", localFileDialogParameters, DialogOptions.FULLSCREEN);
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
                    { x => x.AvailableEmbeddings, this.availableEmbeddingProviders }
                };
        
                var localDirectoryDialogReference = await this.DialogService.ShowAsync<DataSourceLocalDirectoryDialog>("Edit Local Directory Data Source", localDirectoryDialogParameters, DialogOptions.FULLSCREEN);
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
                
                var eriDialogReference = await this.DialogService.ShowAsync<DataSourceERI_V1Dialog>("Edit ERI v1 Data Source", eriDialogParameters, DialogOptions.FULLSCREEN);
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
        var dialogParameters = new DialogParameters
        {
            { "Message", $"Are you sure you want to delete the data source '{dataSource.Name}' of type {dataSource.Type.GetDisplayName()}?" },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>("Delete Data Source", dialogParameters, DialogOptions.FULLSCREEN);
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
                var deleteSecretResponse = await this.RustService.DeleteSecret(externalDataSource);
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

                await this.DialogService.ShowAsync<DataSourceLocalFileInfoDialog>("Local File Data Source Information", localFileDialogParameters, DialogOptions.FULLSCREEN);
                break;
            
            case DataSourceLocalDirectory localDirectory:
                var localDirectoryDialogParameters = new DialogParameters<DataSourceLocalDirectoryInfoDialog>
                {
                    { x => x.DataSource, localDirectory },
                };

                await this.DialogService.ShowAsync<DataSourceLocalDirectoryInfoDialog>("Local Directory Data Source Information", localDirectoryDialogParameters, DialogOptions.FULLSCREEN);
                break;
            
            case DataSourceERI_V1 eriV1DataSource:
                var eriV1DialogParameters = new DialogParameters<DataSourceERI_V1InfoDialog>
                {
                    { x => x.DataSource, eriV1DataSource },
                };
                
                await this.DialogService.ShowAsync<DataSourceERI_V1InfoDialog>("ERI v1 Data Source Information", eriV1DialogParameters, DialogOptions.FULLSCREEN);
                break;
        }
    }
}