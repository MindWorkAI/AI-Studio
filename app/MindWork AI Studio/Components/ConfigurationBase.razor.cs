using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// A base class for configuration options.
/// </summary>
public partial class ConfigurationBase : MSGComponentBase
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
    
    /// <summary>
    /// Is the option locked by a configuration plugin?
    /// </summary>
    [Parameter]
    public Func<bool> IsLocked { get; set; } = () => false;

    protected bool IsDisabled => this.Disabled() || this.IsLocked();
    
    private protected virtual RenderFragment? Body => null;
    
    protected const string MARGIN_CLASS = "mb-6";
    protected static readonly Dictionary<string, object?> SPELLCHECK_ATTRIBUTES = new();
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.SettingsManager.InjectSpellchecking(SPELLCHECK_ATTRIBUTES);
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED ]);
        await base.OnInitializedAsync();
    }

    #endregion
    
    private string TB(string fallbackEN) => this.T(fallbackEN, typeof(ConfigurationBase).Namespace, nameof(ConfigurationBase));
    
    protected async Task InformAboutChange() => await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);

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