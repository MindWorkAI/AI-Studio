using System.Diagnostics.CodeAnalysis;

using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs.Settings;

public abstract class SettingsDialogBase : ComponentBase
{
    [CascadingParameter]
    protected IMudDialogInstance MudDialog { get; set; } = null!;
    
    [Inject]
    protected SettingsManager SettingsManager { get; init; } = null!;
    
    [Inject]
    protected IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    protected MessageBus MessageBus { get; init; } = null!;
    
    
    protected readonly List<ConfigurationSelectData<string>> availableLLMProviders = new();
    
    #region Overrides of ComponentBase

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        this.UpdateProviders();
        base.OnInitialized();
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
}