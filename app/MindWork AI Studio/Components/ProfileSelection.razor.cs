using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ProfileSelection : MSGComponentBase
{
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
    
    private string MarginClass => $"{this.MarginLeft} {this.MarginRight}";
    
    private async Task SelectionChanged(Profile profile)
    {
        this.CurrentProfile = profile;
        await this.CurrentProfileChanged.InvokeAsync(profile);
    }
}