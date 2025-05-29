using System.Diagnostics.CodeAnalysis;

using AIStudio.Components;
using AIStudio.Settings;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs.Settings;

public abstract class SettingsDialogBase : MSGComponentBase
{
    [CascadingParameter]
    protected IMudDialogInstance MudDialog { get; set; } = null!;
    
    [Inject]
    protected IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    protected RustService RustService { get; init; } = null!;
    
    protected readonly List<ConfigurationSelectData<string>> availableLLMProviders = new();
    protected readonly List<ConfigurationSelectData<string>> availableEmbeddingProviders = new();
    
    #region Overrides of ComponentBase

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        this.MudDialog.StateHasChanged();
        
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED ]);
        
        this.UpdateProviders();
        this.UpdateEmbeddingProviders();
    }

    #endregion
    
    protected void Close() => this.MudDialog.Cancel();
    
    [SuppressMessage("Usage", "MWAIS0001:Direct access to `Providers` is not allowed")]
    private void UpdateProviders()
    {
        this.availableLLMProviders.Clear();
        foreach (var provider in this.SettingsManager.ConfigurationData.Providers)
            this.availableLLMProviders.Add(new (provider.InstanceName, provider.Id));
    }
    
    private void UpdateEmbeddingProviders()
    {
        this.availableEmbeddingProviders.Clear();
        foreach (var provider in this.SettingsManager.ConfigurationData.EmbeddingProviders)
            this.availableEmbeddingProviders.Add(new (provider.Name, provider.Id));
    }

    #region Overrides of MSGComponentBase

    protected override Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.CONFIGURATION_CHANGED:
                this.StateHasChanged();
                break;
        }
        
        return Task.CompletedTask;
    }

    #endregion
}