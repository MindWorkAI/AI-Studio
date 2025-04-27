using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

public partial class AssistantBlock<TSettings> : MSGComponentBase where TSettings : IComponent
{
    [Parameter]
    public string Name { get; set; } = string.Empty;
    
    [Parameter]
    public string Description { get; set; } = string.Empty;
    
    [Parameter]
    public string Icon { get; set; } = Icons.Material.Filled.DisabledByDefault;
    
    [Parameter]
    public string ButtonText { get; set; } = "Start";
    
    [Parameter]
    public string Link { get; set; } = string.Empty;
    
    [Inject]
    private MudTheme ColorTheme { get; init; } = null!;
    
    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    private async Task OpenSettingsDialog()
    {
        var dialogParameters = new DialogParameters();
        
        await this.DialogService.ShowAsync<TSettings>(T("Open Settings"), dialogParameters, DialogOptions.FULLSCREEN);
    }
    
    private string BorderColor => this.SettingsManager.IsDarkMode switch
    {
        true => this.ColorTheme.GetCurrentPalette(this.SettingsManager).GrayLight,
        false => this.ColorTheme.GetCurrentPalette(this.SettingsManager).Primary.Value,
    };

    private string BlockStyle => $"border-width: 2px; border-color: {this.BorderColor}; border-radius: 12px; border-style: solid; max-width: 20em;";
}