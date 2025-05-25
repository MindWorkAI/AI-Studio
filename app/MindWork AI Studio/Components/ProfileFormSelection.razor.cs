using AIStudio.Dialogs.Settings;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

public partial class ProfileFormSelection : MSGComponentBase
{
    [Parameter]
    public Profile Profile { get; set; } = Profile.NO_PROFILE;
    
    [Parameter]
    public EventCallback<Profile> ProfileChanged { get; set; }
    
    [Parameter]
    public Func<Profile, string?> Validation { get; set; } = _ => null;
    
    [Inject]
    public IDialogService DialogService { get; init; } = null!;
    
    private async Task SelectionChanged(Profile profile)
    {
        this.Profile = profile;
        await this.ProfileChanged.InvokeAsync(profile);
    }
    
    private async Task OpenSettingsDialog()
    {
        var dialogParameters = new DialogParameters();
        await this.DialogService.ShowAsync<SettingsDialogProfiles>(T("Open Profile Options"), dialogParameters, DialogOptions.FULLSCREEN);
    }
}