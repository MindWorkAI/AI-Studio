using System.Runtime.CompilerServices;
using AIStudio.Settings;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Settings;

public abstract class SettingsPanelBase : ComponentBase
{
    [Parameter]
    public Func<IReadOnlyList<ConfigurationSelectData<string>>> AvailableLLMProvidersFunc { get; set; } = () => [];
    
    [Parameter]
    public SettingsPanel ChosenPanel { get; set; }
    
    protected abstract SettingsPanel Type { get; }
    
    [Inject]
    protected SettingsManager SettingsManager { get; init; } = null!;
    
    [Inject]
    protected IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    protected MessageBus MessageBus { get; init; } = null!;
    
    [Inject]
    protected RustService RustService { get; init; } = null!;

    protected bool IsExtended() => this.Type == this.ChosenPanel;
}