using AIStudio.Dialogs.Settings;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

public partial class ProfileFormSelection : MSGComponentBase
{
    [Parameter]
    public HashSet<string> ProfileIds { get; set; } = [];
    
    [Parameter]
    public EventCallback<HashSet<string>> ProfileIdsChanged { get; set; }
    
    [Parameter]
    public Func<HashSet<string>, string?> Validation { get; set; } = _ => null;

    [Parameter]
    public bool Disabled { get; set; }
    
    [Inject]
    public IDialogService DialogService { get; init; } = null!;
    
    private async Task SelectionChanged(IEnumerable<string?>? profileIds)
    {
        var selection = profileIds is null
            ? []
            : profileIds.Where(profileId => !string.IsNullOrWhiteSpace(profileId)).Select(profileId => profileId!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        this.ProfileIds = selection;
        await this.ProfileIdsChanged.InvokeAsync(selection);
    }

    private string? ValidateSelection(IEnumerable<string?>? profileIds) => this.Validation(
        profileIds is null
            ? []
            : profileIds.Where(profileId => !string.IsNullOrWhiteSpace(profileId)).Select(profileId => profileId!).ToHashSet(StringComparer.OrdinalIgnoreCase));

    private string GetSelectionText(List<string?>? profileIds)
    {
        var profiles = this.SettingsManager.ResolveProfiles(profileIds?.Where(profileId => profileId is not null).Select(profileId => profileId!));
        return profiles.Count switch
        {
            0 => T("No profiles selected"),
            1 => profiles[0].GetSafeName(),
            _ => string.Format(T("{0} profiles"), profiles.Count),
        };
    }
    
    private async Task OpenSettingsDialog()
    {
        var dialogParameters = new DialogParameters();
        await this.DialogService.ShowAsync<SettingsDialogProfiles>(T("Open Profile Options"), dialogParameters, DialogOptions.FULLSCREEN);
    }
}
