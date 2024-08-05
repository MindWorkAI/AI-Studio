using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Blocks;

public partial class ProviderSelection : ComponentBase
{
    [Parameter]
    public Settings.Provider ProviderSettings { get; set; }
    
    [Parameter]
    public EventCallback<Settings.Provider> ProviderSettingsChanged { get; set; }
    
    [Parameter]
    public Func<Settings.Provider, string?> ValidateProvider { get; set; } = _ => null;
    
    [Inject]
    protected SettingsManager SettingsManager { get; set; } = null!;
    
    private async Task SelectionChanged(Settings.Provider provider)
    {
        this.ProviderSettings = provider;
        await this.ProviderSettingsChanged.InvokeAsync(provider);
    }
}