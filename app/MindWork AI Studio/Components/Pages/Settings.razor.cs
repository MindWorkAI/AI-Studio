using AIStudio.Components.CommonDialogs;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Components.CommonDialogs.DialogOptions;

// ReSharper disable ClassNeverInstantiated.Global

namespace AIStudio.Components.Pages;

public partial class Settings : ComponentBase
{
    [Inject]
    public SettingsManager SettingsManager { get; init; } = null!;

    [Inject]
    public IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    public IJSRuntime JsRuntime { get; init; } = null!;
    
    [Inject]
    protected MessageBus MessageBus { get; init; } = null!;

    #region Provider related

    private async Task AddProvider()
    {
        var dialogParameters = new DialogParameters<ProviderDialog>
        {
            { x => x.IsEditing, false },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ProviderDialog>("Add Provider", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var addedProvider = (AIStudio.Settings.Provider)dialogResult.Data!;
        addedProvider = addedProvider with { Num = this.SettingsManager.ConfigurationData.NextProviderNum++ };
        
        this.SettingsManager.ConfigurationData.Providers.Add(addedProvider);
        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task EditProvider(AIStudio.Settings.Provider provider)
    {
        var dialogParameters = new DialogParameters<ProviderDialog>
        {
            { x => x.DataNum, provider.Num },
            { x => x.DataId, provider.Id },
            { x => x.DataInstanceName, provider.InstanceName },
            { x => x.DataProvider, provider.UsedProvider },
            { x => x.DataModel, provider.Model },
            { x => x.DataHostname, provider.Hostname },
            { x => x.IsSelfHosted, provider.IsSelfHosted },
            { x => x.IsEditing, true },
            { x => x.DataHost, provider.Host },
        };

        var dialogReference = await this.DialogService.ShowAsync<ProviderDialog>("Edit Provider", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var editedProvider = (AIStudio.Settings.Provider)dialogResult.Data!;
        
        // Set the provider number if it's not set. This is important for providers
        // added before we started saving the provider number.
        if(editedProvider.Num == 0)
            editedProvider = editedProvider with { Num = this.SettingsManager.ConfigurationData.NextProviderNum++ };
        
        this.SettingsManager.ConfigurationData.Providers[this.SettingsManager.ConfigurationData.Providers.IndexOf(provider)] = editedProvider;
        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task DeleteProvider(AIStudio.Settings.Provider provider)
    {
        var dialogParameters = new DialogParameters
        {
            { "Message", $"Are you sure you want to delete the provider '{provider.InstanceName}'?" },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>("Delete Provider", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var providerInstance = provider.CreateProvider();
        var deleteSecretResponse = await this.SettingsManager.DeleteAPIKey(this.JsRuntime, providerInstance);
        if(deleteSecretResponse.Success)
        {
            this.SettingsManager.ConfigurationData.Providers.Remove(provider);
            await this.SettingsManager.StoreSettings();
        }
        
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }
    
    private bool HasDashboard(Providers provider) => provider switch
    {
        Providers.OPEN_AI => true,
        Providers.MISTRAL => true,
        Providers.ANTHROPIC => true,
        Providers.FIREWORKS => true,
        
        _ => false,
    };
    
    private string GetProviderDashboardURL(Providers provider) => provider switch
    {
        Providers.OPEN_AI => "https://platform.openai.com/usage",
        Providers.MISTRAL => "https://console.mistral.ai/usage/",
        Providers.ANTHROPIC => "https://console.anthropic.com/settings/plans",
        Providers.FIREWORKS => "https://fireworks.ai/account/billing",
        
        _ => string.Empty,
    };

    private string GetProviderModelName(AIStudio.Settings.Provider provider)
    {
        const int MAX_LENGTH = 36;
        var modelName = provider.Model.ToString();
        return modelName.Length > MAX_LENGTH ? "[...] " + modelName[^Math.Min(MAX_LENGTH, modelName.Length)..] : modelName;
    }

    #endregion
}