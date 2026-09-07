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
    public HashSet<string> SelectedProfileIds { get; set; } = [];
    
    [Parameter]
    public EventCallback<HashSet<string>> SelectedProfileIdsChanged { get; set; }

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

    private readonly string defaultToolTipText = TB("You can select your profiles here");

    private IReadOnlyList<Profile> SelectedProfiles => this.SettingsManager.ResolveProfiles(this.SelectedProfileIds);

    private string SelectionLabel => this.SelectedProfiles.Count switch
    {
        0 => string.Empty,
        1 => this.SelectedProfiles[0].GetSafeName(),
        _ => string.Format(TB("{0} profiles"), this.SelectedProfiles.Count),
    };

    private string ToolTipText => this.Disabled
        ? this.DisabledText
        : this.SelectedProfiles.Count > 1
            ? string.Join(", ", this.SelectedProfiles.Select(profile => profile.GetSafeName()))
            : this.defaultToolTipText;
    
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
    
    private async Task SelectionChanged(Profile profile, bool selected)
    {
        var updatedSelection = new HashSet<string>(this.SelectedProfileIds, StringComparer.OrdinalIgnoreCase);
        if (selected)
            updatedSelection.Add(profile.Id);
        else
            updatedSelection.Remove(profile.Id);

        this.SelectedProfileIds = updatedSelection;
        await this.SelectedProfileIdsChanged.InvokeAsync(updatedSelection);
    }

    private async Task ClearSelection()
    {
        this.SelectedProfileIds = [];
        await this.SelectedProfileIdsChanged.InvokeAsync([]);
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
