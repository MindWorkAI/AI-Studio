using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ProfileSelection : MSGComponentBase
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ConfigurationProviderSelection).Namespace, nameof(ConfigurationProviderSelection));
    
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

    private readonly string defaultToolTipText = TB("You can switch between your profiles here");

    private string ToolTipText => this.Disabled ? this.DisabledText : this.defaultToolTipText;
    
    private string MarginClass => $"{this.MarginLeft} {this.MarginRight}";
    
    private async Task SelectionChanged(Profile profile)
    {
        this.CurrentProfile = profile;
        await this.CurrentProfileChanged.InvokeAsync(profile);
    }
}