using AIStudio.Dialogs;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;
using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

/// <summary>
/// A configuration component for capturing and displaying keyboard shortcuts.
/// </summary>
public partial class ConfigurationShortcut : ConfigurationBaseCore
{
    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    /// <summary>
    /// The current shortcut value.
    /// </summary>
    [Parameter]
    public Func<string> Shortcut { get; set; } = () => string.Empty;

    /// <summary>
    /// An action which is called when the shortcut was changed.
    /// </summary>
    [Parameter]
    public Func<string, Task> ShortcutUpdate { get; set; } = _ => Task.CompletedTask;

    /// <summary>
    /// The name/identifier of the shortcut (used for conflict detection and registration).
    /// </summary>
    [Parameter]
    public string ShortcutName { get; set; } = string.Empty;

    /// <summary>
    /// The icon to display.
    /// </summary>
    [Parameter]
    public string Icon { get; set; } = Icons.Material.Filled.Keyboard;

    /// <summary>
    /// The color of the icon.
    /// </summary>
    [Parameter]
    public Color IconColor { get; set; } = Color.Default;

    #region Overrides of ConfigurationBase

    protected override bool Stretch => true;

    protected override Variant Variant => Variant.Outlined;

    protected override string Label => this.OptionDescription;

    #endregion

    private string GetDisplayShortcut()
    {
        var shortcut = this.Shortcut();
        if (string.IsNullOrWhiteSpace(shortcut))
            return string.Empty;

        // Convert internal format to display format
        return shortcut
            .Replace("CmdOrControl", OperatingSystem.IsMacOS() ? "Cmd" : "Ctrl")
            .Replace("CommandOrControl", OperatingSystem.IsMacOS() ? "Cmd" : "Ctrl");
    }

    private async Task OpenDialog()
    {
        var currentShortcut = this.Shortcut();
        var dialogParameters = new DialogParameters<ShortcutDialog>
        {
            { x => x.InitialShortcut, currentShortcut },
            { x => x.ShortcutName, this.ShortcutName },
        };

        var dialogReference = await this.DialogService.ShowAsync<ShortcutDialog>(
            this.T("Configure Keyboard Shortcut"),
            dialogParameters,
            DialogOptions.FULLSCREEN);

        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        if (dialogResult.Data is string newShortcut)
        {
            await this.ShortcutUpdate(newShortcut);
            await this.SettingsManager.StoreSettings();
            await this.InformAboutChange();
        }
    }
}
