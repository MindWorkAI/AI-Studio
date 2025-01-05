using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Settings;

public partial class SettingsPanelAgentContentCleaner : ComponentBase
{
    [Parameter]
    public Func<IReadOnlyList<ConfigurationSelectData<string>>> AvailableLLMProvidersFunc { get; set; } = () => [];
    
    [Inject]
    private SettingsManager SettingsManager { get; init; } = null!;
}