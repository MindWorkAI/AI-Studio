using AIStudio.Assistants;
using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ProviderSelection : ComponentBase
{
    [CascadingParameter]
    public AssistantBase? AssistantBase { get; set; }
    
    [Parameter]
    public AIStudio.Settings.Provider ProviderSettings { get; set; }
    
    [Parameter]
    public EventCallback<AIStudio.Settings.Provider> ProviderSettingsChanged { get; set; }
    
    [Parameter]
    public Func<AIStudio.Settings.Provider, string?> ValidateProvider { get; set; } = _ => null;
    
    [Inject]
    private SettingsManager SettingsManager { get; init; } = null!;
    
    private async Task SelectionChanged(AIStudio.Settings.Provider provider)
    {
        this.ProviderSettings = provider;
        await this.ProviderSettingsChanged.InvokeAsync(provider);
    }
    
    private IEnumerable<AIStudio.Settings.Provider> GetAvailableProviders()
    {
        var minimumLevel = this.SettingsManager.GetMinimumConfidenceLevel(this.AssistantBase?.Component ?? Tools.Components.NONE);
        foreach (var provider in this.SettingsManager.ConfigurationData.Providers)
            if (provider.UsedLLMProvider.GetConfidence(this.SettingsManager).Level >= minimumLevel)
                yield return provider;
    }
}