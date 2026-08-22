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

    private string GetSourceKindLabel(string sourceKind) => sourceKind switch
    {
        "Web content" => T("Web content"),
        "File content" => T("File content"),
        "Chat attachment" => T("Chat attachment"),
        "Retrieval context" => T("Retrieved context"),
        _ => sourceKind,
    };

    private string GetFindingCategoryLabel(string category) => category switch
    {
        "override" => T("Attempt to override instructions"),
        "role_override" => T("Attempt to change the AI's role"),
        "exfiltration" => T("Attempt to expose protected data"),
        "jailbreak" => T("Attempt to bypass safeguards"),
        "agent_manipulation" => T("Attempt to manipulate an agent"),
        "delimiter_evasion" => T("Hidden instructions using delimiters"),
        "markup_evasion" => T("Hidden instructions using markup"),
        "encoding_evasion" => T("Hidden instructions using encoding"),
        "persistence" => T("Persistent or delayed instruction"),
        "evasion" => T("Obfuscated instruction"),
        _ => category,
    };
}