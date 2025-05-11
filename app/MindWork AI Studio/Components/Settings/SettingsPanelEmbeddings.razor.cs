using AIStudio.Dialogs;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components.Settings;

public partial class SettingsPanelEmbeddings : SettingsPanelBase
{
    [Parameter]
    public List<ConfigurationSelectData<string>> AvailableEmbeddingProviders { get; set; } = new();
    
    [Parameter]
    public EventCallback<List<ConfigurationSelectData<string>>> AvailableEmbeddingProvidersChanged { get; set; }
    
    private string GetEmbeddingProviderModelName(EmbeddingProvider provider)
    {
        const int MAX_LENGTH = 36;
        var modelName = provider.Model.ToString();
        return modelName.Length > MAX_LENGTH ? "[...] " + modelName[^Math.Min(MAX_LENGTH, modelName.Length)..] : modelName;
    }

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        await this.UpdateEmbeddingProviders();
        await base.OnInitializedAsync();
    }

    #endregion

    private async Task AddEmbeddingProvider()
    {
        var dialogParameters = new DialogParameters<EmbeddingProviderDialog>
        {
            { x => x.IsEditing, false },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<EmbeddingProviderDialog>(T("Add Embedding Provider"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var addedEmbedding = (EmbeddingProvider)dialogResult.Data!;
        addedEmbedding = addedEmbedding with { Num = this.SettingsManager.ConfigurationData.NextEmbeddingNum++ };
        
        this.SettingsManager.ConfigurationData.EmbeddingProviders.Add(addedEmbedding);
        await this.UpdateEmbeddingProviders();
        
        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }
    
    private async Task EditEmbeddingProvider(EmbeddingProvider embeddingProvider)
    {
        var dialogParameters = new DialogParameters<EmbeddingProviderDialog>
        {
            { x => x.DataNum, embeddingProvider.Num },
            { x => x.DataId, embeddingProvider.Id },
            { x => x.DataName, embeddingProvider.Name },
            { x => x.DataLLMProvider, embeddingProvider.UsedLLMProvider },
            { x => x.DataModel, embeddingProvider.Model },
            { x => x.DataHostname, embeddingProvider.Hostname },
            { x => x.IsSelfHosted, embeddingProvider.IsSelfHosted },
            { x => x.IsEditing, true },
            { x => x.DataHost, embeddingProvider.Host },
        };

        var dialogReference = await this.DialogService.ShowAsync<EmbeddingProviderDialog>(T("Edit Embedding Provider"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var editedEmbeddingProvider = (EmbeddingProvider)dialogResult.Data!;
        
        // Set the provider number if it's not set. This is important for providers
        // added before we started saving the provider number.
        if(editedEmbeddingProvider.Num == 0)
            editedEmbeddingProvider = editedEmbeddingProvider with { Num = this.SettingsManager.ConfigurationData.NextEmbeddingNum++ };
        
        this.SettingsManager.ConfigurationData.EmbeddingProviders[this.SettingsManager.ConfigurationData.EmbeddingProviders.IndexOf(embeddingProvider)] = editedEmbeddingProvider;
        await this.UpdateEmbeddingProviders();
        
        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task DeleteEmbeddingProvider(EmbeddingProvider provider)
    {
        var dialogParameters = new DialogParameters
        {
            { "Message", string.Format(T("Are you sure you want to delete the embedding provider '{0}'?"), provider.Name) },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete Embedding Provider"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var deleteSecretResponse = await this.RustService.DeleteAPIKey(provider);
        if(deleteSecretResponse.Success)
        {
            this.SettingsManager.ConfigurationData.EmbeddingProviders.Remove(provider);
            await this.SettingsManager.StoreSettings();
        }
        
        await this.UpdateEmbeddingProviders();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }
    
    private async Task UpdateEmbeddingProviders()
    {
        this.AvailableEmbeddingProviders.Clear();
        foreach (var provider in this.SettingsManager.ConfigurationData.EmbeddingProviders)
            this.AvailableEmbeddingProviders.Add(new (provider.Name, provider.Id));
        
        await this.AvailableEmbeddingProvidersChanged.InvokeAsync(this.AvailableEmbeddingProviders);
    }
}