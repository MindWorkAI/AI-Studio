using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ProfileFormSelection : ComponentBase
{
    [Parameter]
    public Profile Profile { get; set; } = Profile.NO_PROFILE;
    
    [Parameter]
    public EventCallback<Profile> ProfileChanged { get; set; }
    
    [Parameter]
    public Func<Profile, string?> Validation { get; set; } = _ => null;
    
    [Inject]
    private SettingsManager SettingsManager { get; init; } = null!;
    
    private async Task SelectionChanged(Profile profile)
    {
        this.Profile = profile;
        await this.ProfileChanged.InvokeAsync(profile);
    }
}