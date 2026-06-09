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

    [Inject]
    private RustService RustService { get; init; } = null!;

    /// <summary>
    /// The shortcut binding data.
    /// </summary>
    [Parameter]
    public ConfigurationShortcutData Data { get; set; } = ConfigurationShortcutData.Empty;

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
        var shortcut = this.Data.Value();
        if (string.IsNullOrWhiteSpace(shortcut))
            return string.Empty;

        var shortcutDisplayName = this.Data.DisplayName();
        var shortcutDisplaySource = this.Data.DisplaySource();
        if (!string.IsNullOrWhiteSpace(shortcutDisplayName)
            && string.Equals(shortcutDisplaySource, shortcut, StringComparison.Ordinal))
        {
            return shortcutDisplayName;
        }

        // Convert internal format to display format:
        return shortcut
            .Replace("CmdOrControl", OperatingSystem.IsMacOS() ? "Cmd" : "Ctrl")
            .Replace("CommandOrControl", OperatingSystem.IsMacOS() ? "Cmd" : "Ctrl");
    }

    private async Task OpenDialog()
    {
        // Suspend shortcut processing while the dialog is open, so the user can
        // press the current shortcut to re-enter it without triggering the action:
        await this.RustService.SuspendShortcutProcessing();
        
        try
        {
            var dialogParameters = new DialogParameters<ShortcutDialog>
            {
                { x => x.InitialShortcut, this.Data.Value() },
                { x => x.ShortcutId, this.Data.Id },
            };

            var dialogReference = await this.DialogService.ShowAsync<ShortcutDialog>(
                this.T("Configure Keyboard Shortcut"),
                dialogParameters,
                DialogOptions.FULLSCREEN);

            var dialogResult = await dialogReference.Result;
            if (dialogResult is null || dialogResult.Canceled)
                return;

            if (dialogResult.Data is ShortcutDialogResult shortcutResult)
            {
                this.Data.ValueUpdate(shortcutResult.Shortcut);
                this.Data.DisplayUpdate(shortcutResult.DisplayName, shortcutResult.DisplaySource);
                await this.SettingsManager.StoreSettings();
                await this.InformAboutChange();
            }
            else if (dialogResult.Data is string newShortcut)
            {
                this.Data.ValueUpdate(newShortcut);
                this.Data.DisplayUpdate(string.Empty, string.Empty);
                await this.SettingsManager.StoreSettings();
                await this.InformAboutChange();
            }
        }
        finally
        {
            // Resume the shortcut processing when the dialog is closed:
            await this.RustService.ResumeShortcutProcessing();
        }
    }
}
