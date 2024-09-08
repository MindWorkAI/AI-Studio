using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ProfileSelection : ComponentBase
{
    [Parameter]
    public Profile CurrentProfile { get; set; } = Profile.NO_PROFILE;
    
    [Parameter]
    public EventCallback<Profile> CurrentProfileChanged { get; set; }

    [Parameter]
    public string MarginLeft { get; set; } = "ml-3";
    
    [Inject]
    private SettingsManager SettingsManager { get; init; } = null!;
    
    private string MarginClass => $"{this.MarginLeft}";
    
    private async Task SelectionChanged(Profile profile)
    {
        this.CurrentProfile = profile;
        await this.CurrentProfileChanged.InvokeAsync(profile);
    }
}