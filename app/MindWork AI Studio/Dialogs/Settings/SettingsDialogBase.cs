using System.Diagnostics.CodeAnalysis;

using AIStudio.Settings;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs.Settings;

public abstract class SettingsDialogBase : ComponentBase, IMessageBusReceiver, IDisposable
{
    [CascadingParameter]
    protected IMudDialogInstance MudDialog { get; set; } = null!;
    
    [Inject]
    protected SettingsManager SettingsManager { get; init; } = null!;
    
    [Inject]
    protected IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    protected MessageBus MessageBus { get; init; } = null!;
    
    [Inject]
    protected RustService RustService { get; init; } = null!;
    
    protected readonly List<ConfigurationSelectData<string>> availableLLMProviders = new();
    protected readonly List<ConfigurationSelectData<string>> availableEmbeddingProviders = new();
    
    #region Overrides of ComponentBase

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        // Register this component with the message bus:
        this.MessageBus.RegisterComponent(this);
        this.MessageBus.ApplyFilters(this, [], [ Event.CONFIGURATION_CHANGED ]);
        
        this.UpdateProviders();
        this.UpdateEmbeddingProviders();
        await base.OnInitializedAsync();
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
    
    #region Implementation of IMessageBusReceiver

    public string ComponentName => nameof(Settings);
    
    public Task ProcessMessage<TMsg>(ComponentBase? sendingComponent, Event triggeredEvent, TMsg? data)
    {
        switch (triggeredEvent)
        {
            case Event.CONFIGURATION_CHANGED:
                this.StateHasChanged();
                break;
        }

        return Task.CompletedTask;
    }

    public Task<TResult?> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data)
    {
        return Task.FromResult<TResult?>(default);
    }

    #endregion

    #region Implementation of IDisposable

    public void Dispose()
    {
        this.MessageBus.Unregister(this);
    }

    #endregion
}