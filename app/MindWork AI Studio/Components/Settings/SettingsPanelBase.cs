using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Settings;

public abstract class SettingsPanelBase : ComponentBase
{
    [Parameter]
    public Func<IReadOnlyList<ConfigurationSelectData<string>>> AvailableLLMProvidersFunc { get; set; } = () => [];
    
    [Inject]
    protected SettingsManager SettingsManager { get; init; } = null!;
    
    [Inject]
    protected IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    protected MessageBus MessageBus { get; init; } = null!;
    
    [Inject]
    protected RustService RustService { get; init; } = null!;
}