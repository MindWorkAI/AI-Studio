using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ProfilePreselectionConfiguration : MSGComponentBase
{
    private ProfilePreselectionMode? selectedModeOverride;

    [Parameter]
    public string OptionDescription { get; set; } = string.Empty;

    [Parameter]
    public string OptionHelp { get; set; } = string.Empty;

    [Parameter]
    public Func<bool> Disabled { get; set; } = () => false;

    [Parameter]
    public Func<bool> IsLocked { get; set; } = () => false;

    [Parameter]
    public Func<HashSet<string>?> SelectedProfileIds { get; set; } = () => null;

    [Parameter]
    public Action<HashSet<string>?> SelectionUpdate { get; set; } = _ => { };

    [Parameter]
    public Func<HashSet<string>?, Task> SelectionUpdateAsync { get; set; } = _ => Task.CompletedTask;

    private ProfilePreselectionMode SelectedMode => this.selectedModeOverride ?? ProfilePreselection.FromStoredValue(this.SelectedProfileIds()).Mode;

    private HashSet<string> SpecificProfileIds => this.SelectedProfileIds() ?? [];

    private async Task ModeChanged(ProfilePreselectionMode mode)
    {
        this.selectedModeOverride = mode;
        HashSet<string>? selection = mode switch
        {
            ProfilePreselectionMode.USE_APP_DEFAULT => null,
            ProfilePreselectionMode.USE_NO_PROFILES => [],
            ProfilePreselectionMode.USE_SPECIFIC_PROFILES => this.SpecificProfileIds.Count > 0 ? [..this.SpecificProfileIds] : [],
            _ => null,
        };

        await this.UpdateSelection(selection);
        await this.InvokeAsync(this.StateHasChanged);
    }

    private async Task ProfilesChanged(HashSet<string> profileIds) => await this.UpdateSelection(profileIds.ToHashSet(StringComparer.OrdinalIgnoreCase));

    private async Task UpdateSelection(HashSet<string>? selection)
    {
        this.SelectionUpdate(selection);
        await this.SelectionUpdateAsync(selection);
    }

    private string GetSelectionText(List<string?>? profileIds)
    {
        var profiles = this.SettingsManager.ResolveProfiles(profileIds?.OfType<string>());
        return profiles.Count == 0 ? T("No profiles selected") : string.Join(", ", profiles.Select(profile => profile.GetSafeName()));
    }

    private IEnumerable<ConfigurationSelectData<ProfilePreselectionMode>> GetModeData()
    {
        yield return new(T("Use app default"), ProfilePreselectionMode.USE_APP_DEFAULT);
        yield return new(T("Use no profiles"), ProfilePreselectionMode.USE_NO_PROFILES);
        yield return new(T("Use a custom profile selection"), ProfilePreselectionMode.USE_SPECIFIC_PROFILES);
    }

    private string ProfileIcon(string profileId) => this.SettingsManager.GetProfileById(profileId).IsEnterpriseConfiguration
        ? Icons.Material.Filled.Business
        : Icons.Material.Filled.Person4;
}
