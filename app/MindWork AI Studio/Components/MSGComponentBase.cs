using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

using Microsoft.AspNetCore.Components;

using SharedTools;

namespace AIStudio.Components;

public abstract class MSGComponentBase : ComponentBase, IDisposable, IMessageBusReceiver, ILang
{
    [Inject]
    protected SettingsManager SettingsManager { get; init; } = null!;
    
    [Inject]
    protected MessageBus MessageBus { get; init; } = null!;

    [Inject]
    private ILogger<PluginLanguage> Logger { get; init; } = null!;

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
    public string T(string fallbackEN)
    {
        var type = this.GetType();
        var ns = $"{type.Namespace!}::{type.Name}".ToUpperInvariant().Replace(".", "::");
        var key = $"root::{ns}::T{fallbackEN.ToFNV32()}";
        
        if(this.Lang.TryGetText(key, out var text, logWarning: false))
            return text;
        
        this.Logger.LogWarning($"Missing translation key '{key}' for content '{fallbackEN}'.");
        return fallbackEN;
    }

    #endregion

    #region Implementation of IMessageBusReceiver

    public abstract string ComponentName { get; }

    public async Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data)
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
            
            default:
                await this.ProcessIncomingMessage(sendingComponent, triggeredEvent, data);
                break;
        }
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

    #region Implementation of IDisposable

    public void Dispose()
    {
        this.MessageBus.Unregister(this);
    }

    #endregion
    
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
        
        this.MessageBus.ApplyFilters(this, filterComponents, eventsList.ToArray());
    }
}