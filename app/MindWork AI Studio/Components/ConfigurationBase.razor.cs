using AIStudio.Settings;
using AIStudio.Tools;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// A base class for configuration options.
/// </summary>
public partial class ConfigurationBase : ComponentBase
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
    
    [Inject]
    protected SettingsManager SettingsManager { get; init; } = null!;
    
    [Inject]
    protected MessageBus MessageBus { get; init; } = null!;
    
    protected const string MARGIN_CLASS = "mb-6";

    protected async Task InformAboutChange() => await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
}