using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public abstract class MSGComponentBase : ComponentBase, IDisposable, IAsyncDisposable, IMessageBusReceiver, ILang
{
    [Inject]
    protected SettingsManager SettingsManager { get; init; } = null!;
    
    [Inject]
    protected MessageBus MessageBus { get; init; } = null!;

    private ILanguagePlugin Lang { get; set; } = PluginFactory.BaseLanguage;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.Lang = await this.SettingsManager.GetActiveLanguagePlugin();
        
        this.MessageBus.RegisterComponent(this);
        await base.OnInitializedAsync();
    }

    #endregion

    #region Implementation of ILang

    /// <inheritdoc />
    public string T(string fallbackEN) => this.GetText(this.Lang, fallbackEN);
    
    /// <inheritdoc />
    public string T(string fallbackEN, string? typeNamespace, string? typeName) => this.GetText(this.Lang, fallbackEN, typeNamespace, typeName);

    #endregion

    #region Implementation of IMessageBusReceiver

    public async Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data)
    {
        await this.InvokeAsync(async () =>
        {
            switch (triggeredEvent)
            {
                case Event.COLOR_THEME_CHANGED:
                    this.StateHasChanged();
                    break;
            
                case Event.PLUGINS_RELOADED:
                    this.Lang = await this.SettingsManager.GetActiveLanguagePlugin();
                    await this.InvokeAsync(this.StateHasChanged);
                    break;
            }
        
            await this.ProcessIncomingMessage(sendingComponent, triggeredEvent, data);
        });
    }

    public async Task<TResult?> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data)
    {
        return await this.ProcessIncomingMessageWithResult<TPayload, TResult>(sendingComponent, triggeredEvent, data);
    }

    #endregion

    protected virtual Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data)
    {
        return Task.CompletedTask;
    }

    protected virtual Task<TResult?> ProcessIncomingMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data)
    {
        return Task.FromResult<TResult?>(default);
    }
    
    protected async Task SendMessage<T>(Event triggeredEvent, T? data = default)
    {
        await this.MessageBus.SendMessage(this, triggeredEvent, data);
    }
    
    protected async Task<TResult?> SendMessageWithResult<TPayload, TResult>(Event triggeredEvent, TPayload? data)
    {
        return await this.MessageBus.SendMessageUseFirstResult<TPayload, TResult>(this, triggeredEvent, data);
    }
    
    /// <summary>
    /// Define for which components and events you want to receive messages.
    /// </summary>
    /// <param name="filterComponents">A list of components for which you want to receive messages. Use an empty list to receive messages from all components.</param>
    /// <param name="events">A list of events for which you want to receive messages.</param>
    protected void ApplyFilters(ComponentBase[] filterComponents, Event[] events)
    {
        // Append the color theme changed event to the list of events:
        var eventsList = new List<Event>(events)
        {
            Event.COLOR_THEME_CHANGED,
            Event.PLUGINS_RELOADED,
        };
        
        this.MessageBus.ApplyFilters(this, filterComponents, eventsList.ToHashSet());
    }
    
    /// <summary>
    /// Releases what this component has acquired. Override this instead of implementing
    /// IDisposable again, so the deregistration from the message bus cannot be lost.
    /// </summary>
    protected virtual void DisposeResources()
    {
    }

    /// <summary>
    /// Releases what this component has acquired and needs an await to release. Override this
    /// instead of implementing IAsyncDisposable, see the remarks on DisposeAsync below.
    /// </summary>
    protected virtual ValueTask DisposeResourcesAsync() => ValueTask.CompletedTask;

    #region Implementation of IDisposable

    public void Dispose()
    {
        this.MessageBus.Unregister(this);
        this.DisposeResources();
    }

    #endregion

    #region Implementation of IAsyncDisposable

    /// <summary>
    /// Releases this component asynchronously.
    /// </summary>
    /// <remarks>
    /// This base class implements both ways of disposing on purpose. Blazor calls only DisposeAsync
    /// when a component offers both, so a derived component which implements IAsyncDisposable on
    /// its own would silently skip everything Dispose does — above all the deregistration from the
    /// message bus, which holds a strong reference to every receiver. Deriving components override
    /// DisposeResources or DisposeResourcesAsync instead, and this stays the one place which knows
    /// about both.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        await this.DisposeResourcesAsync();
        this.Dispose();
    }

    #endregion
}