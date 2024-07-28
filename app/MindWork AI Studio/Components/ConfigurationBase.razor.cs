using AIStudio.Settings;
using AIStudio.Tools;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// A base class for configuration options.
/// </summary>
public partial class ConfigurationBase : ComponentBase, IMessageBusReceiver, IDisposable
{
    /// <summary>
    /// The description of the option, i.e., the name. Should be
    /// as short as possible.
    /// </summary>
    [Parameter]
    public string OptionDescription { get; set; } = string.Empty;
    
    /// <summary>
    /// A helpful text that explains the option in more detail.
    /// </summary>
    [Parameter]
    public string OptionHelp { get; set; } = string.Empty;
    
    /// <summary>
    /// Is the option disabled?
    /// </summary>
    [Parameter]
    public Func<bool> Disabled { get; set; } = () => false;
    
    [Inject]
    protected SettingsManager SettingsManager { get; init; } = null!;
    
    [Inject]
    protected MessageBus MessageBus { get; init; } = null!;
    
    protected const string MARGIN_CLASS = "mb-6";
    protected static readonly Dictionary<string, object?> SPELLCHECK_ATTRIBUTES = new();
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Configure the spellchecking for the instance name input:
        this.SettingsManager.InjectSpellchecking(SPELLCHECK_ATTRIBUTES);
        
        // Register this component with the message bus:
        this.MessageBus.RegisterComponent(this);
        this.MessageBus.ApplyFilters(this, [], [ Event.CONFIGURATION_CHANGED ]);
        
        await base.OnInitializedAsync();
    }

    #endregion

    protected async Task InformAboutChange() => await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    
    #region Implementation of IMessageBusReceiver

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