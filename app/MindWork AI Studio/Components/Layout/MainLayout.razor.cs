using AIStudio.Settings;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AIStudio.Components.Layout;

public partial class MainLayout : LayoutComponentBase
{
    [Inject]
    private IJSRuntime JsRuntime { get; set; } = null!;
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        var dataDir = await this.JsRuntime.InvokeAsync<string>("window.__TAURI__.path.appLocalDataDir");
        var configDir = await this.JsRuntime.InvokeAsync<string>("window.__TAURI__.path.appConfigDir");
        SettingsManager.ConfigDirectory = configDir;
        SettingsManager.DataDirectory = dataDir;
        
        await base.OnInitializedAsync();
    }

    #endregion
}