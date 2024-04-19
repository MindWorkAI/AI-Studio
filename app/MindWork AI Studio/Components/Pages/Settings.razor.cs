using AIStudio.Settings;
using Microsoft.AspNetCore.Components;

using MudBlazor;

// ReSharper disable ClassNeverInstantiated.Global

namespace AIStudio.Components.Pages;

public partial class Settings : ComponentBase
{
    [Inject]
    public SettingsManager SettingsManager { get; init; } = null!;

    [Inject]
    public IDialogService DialogService { get; init; } = null!;

    private List<global::AIStudio.Settings.Provider> Providers { get; set; } = new();

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        var settings = await this.SettingsManager.LoadSettings();
        this.Providers = settings.Providers;
        
        await base.OnInitializedAsync();
    }

    #endregion
    
    private async Task AddProvider()
    {
        var dialogOptions = new DialogOptions { CloseOnEscapeKey = true, FullWidth = true, MaxWidth = MaxWidth.Medium };
        var dialogReference = await this.DialogService.ShowAsync<ProviderDialog>("Add Provider", dialogOptions);
        var dialogResult = await dialogReference.Result;
        if (dialogResult.Canceled)
            return;

        var addedProvider = (AIStudio.Settings.Provider)dialogResult.Data;
        this.Providers.Add(addedProvider);
    }
}