using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Pages;

public partial class Settings : ComponentBase, IMessageBusReceiver, IDisposable
{
    [Inject]
    private SettingsManager SettingsManager { get; init; } = null!;
    
    [Inject]
    private MessageBus MessageBus { get; init; } = null!;
    
    private List<ConfigurationSelectData<string>> availableLLMProviders = new();
    private List<ConfigurationSelectData<string>> availableEmbeddingProviders = new();
    private List<ConfigurationSelectData<string>> availableDataSources = new();

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Register this component with the message bus:
        this.MessageBus.RegisterComponent(this);
        this.MessageBus.ApplyFilters(this, [], [ Event.CONFIGURATION_CHANGED ]);
        
        await base.OnInitializedAsync();
    }

    #endregion
    
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