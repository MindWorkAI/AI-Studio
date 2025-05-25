using AIStudio.Dialogs.Settings;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

public partial class ProfileSelection : MSGComponentBase
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ProfileSelection).Namespace, nameof(ProfileSelection));
    
    [Parameter]
    public Profile CurrentProfile { get; set; } = Profile.NO_PROFILE;
    
    [Parameter]
    public EventCallback<Profile> CurrentProfileChanged { get; set; }

    [Parameter]
    public string MarginLeft { get; set; } = "ml-3";

    [Parameter]
    public string MarginRight { get; set; } = string.Empty;
    
    [Parameter]
    public bool Disabled { get; set; }
    
    [Parameter]
    public string DisabledText { get; set; } = string.Empty;
    
    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    private readonly string defaultToolTipText = TB("You can switch between your profiles here");

    private string ToolTipText => this.Disabled ? this.DisabledText : this.defaultToolTipText;
    
    private string MarginClass => $"{this.MarginLeft} {this.MarginRight}";
    
    private async Task SelectionChanged(Profile profile)
    {
        this.CurrentProfile = profile;
        await this.CurrentProfileChanged.InvokeAsync(profile);
    }

    private async Task OpenSettingsDialog()
    {
        var dialogParameters = new DialogParameters();
        await this.DialogService.ShowAsync<SettingsDialogProfiles>(T("Open Profile Options"), dialogParameters, DialogOptions.FULLSCREEN);
    }
}