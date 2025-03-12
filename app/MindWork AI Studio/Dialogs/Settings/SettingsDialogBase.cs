using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using AIStudio.Settings;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs.Settings;

public abstract class SettingsDialogBase : ComponentBase
{
    [CascadingParameter]
    protected MudDialogInstance MudDialog { get; set; } = null!;
    
    [Parameter]
    public List<ConfigurationSelectData<string>> AvailableLLMProviders { get; set; } = new();
    
    [Inject]
    protected SettingsManager SettingsManager { get; init; } = null!;
    
    [Inject]
    protected IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    protected MessageBus MessageBus { get; init; } = null!;
    
    [Inject]
    protected RustService RustService { get; init; } = null!;
    
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
        this.AvailableLLMProviders.Clear();
        foreach (var provider in this.SettingsManager.ConfigurationData.Providers)
            this.AvailableLLMProviders.Add(new (provider.InstanceName, provider.Id));
    }
}