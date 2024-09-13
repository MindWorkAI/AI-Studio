using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ProviderSelection : ComponentBase
{
    [Parameter]
    public Settings.Provider ProviderSettings { get; set; }
    
    [Parameter]
    public EventCallback<Settings.Provider> ProviderSettingsChanged { get; set; }
    
    [Parameter]
    public Func<Settings.Provider, string?> ValidateProvider { get; set; } = _ => null;
    
    [Inject]
    private SettingsManager SettingsManager { get; init; } = null!;
    
    private async Task SelectionChanged(Settings.Provider provider)
    {
        this.ProviderSettings = provider;
        await this.ProviderSettingsChanged.InvokeAsync(provider);
    }
    
    private IEnumerable<Settings.Provider> GetAvailableProviders()
    {
        if (this.SettingsManager.ConfigurationData.LLMProviders is { EnforceGlobalMinimumConfidence: true, GlobalMinimumConfidence: not ConfidenceLevel.NONE and not ConfidenceLevel.UNKNOWN })
        {
            var minimumLevel = this.SettingsManager.ConfigurationData.LLMProviders.GlobalMinimumConfidence;
            foreach (var provider in this.SettingsManager.ConfigurationData.Providers)
                if (provider.UsedLLMProvider.GetConfidence(this.SettingsManager).Level >= minimumLevel)
                    yield return provider;
        }
        else
        {
            foreach (var provider in this.SettingsManager.ConfigurationData.Providers)
                yield return provider;
        }
    }
}