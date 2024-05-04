using AIStudio.Components.CommonDialogs;
using AIStudio.Provider;
using AIStudio.Settings;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using MudBlazor;

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

    private static readonly DialogOptions DIALOG_OPTIONS = new()
    {
        CloseOnEscapeKey = true,
        FullWidth = true, MaxWidth = MaxWidth.Medium,
    };

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        await this.SettingsManager.LoadSettings();
        await base.OnInitializedAsync();
    }

    #endregion

    #region Provider related

    private async Task AddProvider()
    {
        var dialogParameters = new DialogParameters<ProviderDialog>
        {
            { x => x.IsEditing, false },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ProviderDialog>("Add Provider", dialogParameters, DIALOG_OPTIONS);
        var dialogResult = await dialogReference.Result;
        if (dialogResult.Canceled)
            return;

        var addedProvider = (AIStudio.Settings.Provider)dialogResult.Data;
        this.SettingsManager.ConfigurationData.Providers.Add(addedProvider);
        await this.SettingsManager.StoreSettings();
    }

    private async Task EditProvider(global::AIStudio.Settings.Provider provider)
    {
        var dialogParameters = new DialogParameters<ProviderDialog>
        {
            { x => x.DataId, provider.Id },
            { x => x.DataInstanceName, provider.InstanceName },
            { x => x.DataProvider, provider.UsedProvider },
            { x => x.IsEditing, true },
        };

        var dialogReference = await this.DialogService.ShowAsync<ProviderDialog>("Edit Provider", dialogParameters, DIALOG_OPTIONS);
        var dialogResult = await dialogReference.Result;
        if (dialogResult.Canceled)
            return;

        var editedProvider = (AIStudio.Settings.Provider)dialogResult.Data;
        this.SettingsManager.ConfigurationData.Providers[this.SettingsManager.ConfigurationData.Providers.IndexOf(provider)] = editedProvider;
        await this.SettingsManager.StoreSettings();
    }

    private async Task DeleteProvider(global::AIStudio.Settings.Provider provider)
    {
        var dialogParameters = new DialogParameters
        {
            { "Message", $"Are you sure you want to delete the provider '{provider.InstanceName}'?" },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>("Delete Provider", dialogParameters, DIALOG_OPTIONS);
        var dialogResult = await dialogReference.Result;
        if (dialogResult.Canceled)
            return;
        
        var providerInstance = provider.UsedProvider.CreateProvider();
        providerInstance.InstanceName = provider.InstanceName;
        
        var deleteSecretResponse = await this.SettingsManager.DeleteAPIKey(this.JsRuntime, providerInstance);
        if(deleteSecretResponse.Success)
        {
            this.SettingsManager.ConfigurationData.Providers.Remove(provider);
            await this.SettingsManager.StoreSettings();
        }
    }

    #endregion
}