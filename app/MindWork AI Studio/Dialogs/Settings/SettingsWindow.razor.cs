using AIStudio.Components.Settings;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs.Settings;

/// <summary>
/// The window of the settings: a list of panes on the left, and the options of the chosen pane on the
/// right.
/// </summary>
/// <remarks>
/// <para>
/// Every place which offers settings opens this window at the path of what it offers, see
/// <see cref="OpenAsync"/>, so the user always finds the settings in the same window. A pane below the
/// first level opens in place of its parent; a back button left of its title leads one level up, and
/// the list on the left keeps the pane of the first level marked meanwhile.
/// </para>
/// <para>
/// So far, the window holds the options of the chat, the chat templates, and the options of the
/// workspaces. The settings page and the settings dialogs of the assistants are meant to move into it
/// as further panes, see <see cref="SettingsPanes"/>.
/// </para>
/// </remarks>
public partial class SettingsWindow : SettingsDialogBase
{
    /// <summary>
    /// The path of the pane the window opens with.
    /// </summary>
    [Parameter]
    public SettingsPath Path { get; set; } = SettingsPath.CHAT;

    private SettingsPane pane = SettingsPanes.ALL[0];
    private IReadOnlyList<SettingsPane> trail = [SettingsPanes.ALL[0]];
    private Dictionary<string, object> paneParameters = new();

    /// <summary>
    /// Opens the settings window at a pane.
    /// </summary>
    /// <param name="dialogService">The dialog service of the caller.</param>
    /// <param name="path">The path of the pane to open. A path which leads nowhere, or to a pane which cannot be shown right now, opens the nearest pane above it, see <see cref="SettingsPanes.Resolve"/>.</param>
    public static async Task OpenAsync(IDialogService dialogService, SettingsPath path)
    {
        var parameters = new DialogParameters<SettingsWindow>
        {
            { x => x.Path, path },
        };

        await dialogService.ShowAsync<SettingsWindow>(null, parameters, DialogOptions.SETTINGS_WINDOW);
    }

    #region Overrides of SettingsDialogBase

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        //
        // Every pane gets what a panel of the settings page gets. The list of providers is read when
        // the pane asks for it, since the window keeps it current while it is open:
        //
        this.paneParameters = new()
        {
            [nameof(SettingsPanelBase.AvailableLLMProvidersFunc)] = (Func<IReadOnlyList<ConfigurationSelectData<string>>>)(() => this.AvailableLLMProviders),
        };

        this.Show(this.Path);
        await base.OnInitializedAsync();
    }

    #endregion

    /// <summary>
    /// Shows another pane in this window.
    /// </summary>
    /// <remarks>
    /// A pane which leads to another one, such as a list of assistants leading to the options of one of
    /// them, calls this through the window it gets as a cascading value, instead of opening a second
    /// window.
    /// </remarks>
    /// <param name="path">The path of the pane to show.</param>
    public void NavigateTo(SettingsPath path)
    {
        this.Show(path);
        this.StateHasChanged();
    }

    /// <summary>
    /// The path of the pane of the first level which holds the pane shown, marked in the list.
    /// </summary>
    private SettingsPath FirstLevelPath => this.trail[0].Path;

    /// <summary>
    /// The pane one level above the pane shown, or null for a pane of the first level.
    /// </summary>
    private SettingsPane? ParentPane => this.trail.Count > 1 ? this.trail[^2] : null;

    /// <summary>
    /// What the back button of a deeper pane says it does: where it leads.
    /// </summary>
    /// <param name="parentPane">The pane one level up.</param>
    private string GetBackText(SettingsPane parentPane) => string.Format(T("Back to {0}"), parentPane.Title());

    private void Show(SettingsPath path)
    {
        this.pane = SettingsPanes.Resolve(path, this.IsAvailable);
        this.trail = SettingsPanes.Trail(this.pane);
    }

    private bool IsAvailable(SettingsPane candidate) => candidate.IsAvailable?.Invoke(this.SettingsManager) ?? true;
}