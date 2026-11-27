using AIStudio.Dialogs;
using AIStudio.Tools.PluginSystem.Assistants;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components.Settings;

public partial class SettingsPanelAITaskAssistantAudit : SettingsPanelBase
{
    [Inject]
    private AssistantPluginAuditService AssistantPluginAuditService { get; init; } = null!;

    /// <summary>
    /// Whether automatic audits are switched on, although no provider is left for them.
    /// </summary>
    /// <remarks>
    /// Without a provider, the automatic audit skips every plugin. The least any audit requires decides here:
    /// should no provider meet it, none meets what a particular plugin requires either.
    /// </remarks>
    private bool IsAutomaticAuditWithoutProvider => this.SettingsManager.ConfigurationData.AssistantPluginAudit.AutomaticallyAuditAssistants && !this.AssistantPluginAuditService.PreviewProvider().HasProvider;
    /// <summary>
    /// Asks the user before they turn off the audit before activation; turning it on needs no confirmation.
    /// </summary>
    /// <param name="updatedState">The state the user chose.</param>
    /// <returns>True when the option may take the state.</returns>
    private async Task<bool> ConfirmRequireAuditBeforeActivationChange(bool updatedState)
    {
        if (updatedState)
            return true;

        var dialogParameters = new DialogParameters<ConfirmDialog>
        {
            {
                x => x.Message,
                TA("Disabling this setting turns off assistant plugin security audits. External assistants may then be activated and used even without a valid audit or after plugin changes. Do you really want to disable this protection?")
            },
        };

        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(
            T("Disable Assistant Audit Protection"),
            dialogParameters,
            DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        return dialogResult is { Canceled: false };
    }
}