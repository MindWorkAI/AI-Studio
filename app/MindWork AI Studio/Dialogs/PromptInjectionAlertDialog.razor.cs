using AIStudio.Components;
using AIStudio.Settings;
using AIStudio.Tools.Security;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class PromptInjectionAlertDialog : MSGComponentBase
{
    private bool showPromptInjectionInformation;

    private static bool CanDisableFutureAlerts => !ManagedConfiguration.TryGet(x => x.App, x => x.ShowPromptInjectionAlert, out var meta) || !meta.IsLocked;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// What was filtered during the user action that triggered this dialog.
    /// </summary>
    /// <remarks>
    /// Carries every affected source, because one action may involve many documents and the
    /// user should acknowledge them together rather than one dialog at a time.
    /// </remarks>
    [Parameter, EditorRequired]
    public PromptInjectionAlertMessage Alert { get; set; } = null!;

    private void Close() => this.MudDialog.Close();

    private async Task CloseAndDisableFutureAlertsAsync()
    {
        this.SettingsManager.ConfigurationData.App.ShowPromptInjectionAlert = false;
        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
        this.MudDialog.Close();
    }

    private void TogglePromptInjectionInformation() => this.showPromptInjectionInformation = !this.showPromptInjectionInformation;
}
