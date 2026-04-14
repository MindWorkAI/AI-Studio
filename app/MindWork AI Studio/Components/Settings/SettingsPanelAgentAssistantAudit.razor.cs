using AIStudio.Dialogs;
using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components.Settings;

public partial class SettingsPanelAgentAssistantAudit : SettingsPanelBase
{
    private async Task RequireAuditBeforeActivationChanged(bool updatedState)
    {
        if (!updatedState)
        {
            var dialogParameters = new DialogParameters<ConfirmDialog>
            {
                {
                    x => x.Message,
                    this.T("Disabling this setting turns off assistant plugin security audits. External assistants may then be activated and used even without a valid audit or after plugin changes. Do you really want to disable this protection?")
                },
            };

            var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(
                this.T("Disable Assistant Audit Protection"),
                dialogParameters,
                DialogOptions.FULLSCREEN);
            var dialogResult = await dialogReference.Result;
            if (dialogResult is null || dialogResult.Canceled)
            {
                await this.InvokeAsync(this.StateHasChanged);
                return;
            }
        }

        this.SettingsManager.ConfigurationData.AssistantPluginAudit.RequireAuditBeforeActivation = updatedState;
        await this.SettingsManager.StoreSettings();
        await this.SendMessage<bool>(Event.CONFIGURATION_CHANGED);
        await this.InvokeAsync(this.StateHasChanged);
    }
}
