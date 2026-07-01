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

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED ]);
        await base.OnInitializedAsync();
    }

    #endregion
    
    private string ProfileIcon(Profile profile)
    {
        if (profile.IsEnterpriseConfiguration)
            return Icons.Material.Filled.Business;
        
        return Icons.Material.Filled.Person4;
    }
    
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

    #region Overrides of MSGComponentBase

    protected override Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        if (triggeredEvent is Event.CONFIGURATION_CHANGED or Event.PLUGINS_RELOADED)
            this.StateHasChanged();

        return Task.CompletedTask;
    }

    #endregion
}