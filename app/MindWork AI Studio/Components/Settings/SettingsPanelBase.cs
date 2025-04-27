using AIStudio.Settings;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Settings;

public abstract class SettingsPanelBase : MSGComponentBase
{
    [Parameter]
    public Func<IReadOnlyList<ConfigurationSelectData<string>>> AvailableLLMProvidersFunc { get; set; } = () => [];
    
    [Inject]
    protected IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    protected RustService RustService { get; init; } = null!;
}