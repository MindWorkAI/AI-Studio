using AIStudio.Components;
using AIStudio.Agents.AssistantAudit;
using AIStudio.Dialogs;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem.Assistants;
using AIStudio.Tools.PluginSystem;

using Microsoft.AspNetCore.Components;
using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Pages;

public partial class Plugins : MSGComponentBase
{
    private const string GROUP_ENABLED = "Enabled";
    private const string GROUP_DISABLED = "Disabled";
    private const string GROUP_INTERNAL = "Internal";

    private DataAssistantPluginAudit AssistantPluginAuditSettings => this.SettingsManager.ConfigurationData.AssistantPluginAudit;
    
    private TableGroupDefinition<IPluginMetadata> groupConfig = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.PLUGINS_RELOADED ]);
        
        this.groupConfig = new TableGroupDefinition<IPluginMetadata>
        {
            Expandable = true,
            IsInitiallyExpanded = true,
            Selector = pluginMeta =>
            {
                if (pluginMeta.IsInternal)
                    return GROUP_INTERNAL;
                
                return this.SettingsManager.IsPluginEnabled(pluginMeta)
                    ? GROUP_ENABLED
                    : GROUP_DISABLED;
            }
        };
        
        await base.OnInitializedAsync();
    }

    #endregion

    private async Task PluginActivationStateChanged(IPluginMetadata pluginMeta)
    {
        if (this.SettingsManager.IsPluginEnabled(pluginMeta))
        {
            this.SettingsManager.ConfigurationData.EnabledPlugins.Remove(pluginMeta.Id);
            await this.SettingsManager.StoreSettings();
            await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
            return;
        }

        if (pluginMeta.Type is not PluginType.ASSISTANT || !this.AssistantPluginAuditSettings.RequireAuditBeforeActivation)
        {
            this.SettingsManager.ConfigurationData.EnabledPlugins.Add(pluginMeta.Id);
            await this.SettingsManager.StoreSettings();
            await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
            return;
        }

        var assistantPlugin = PluginFactory.RunningPlugins.OfType<PluginAssistants>().FirstOrDefault(x => x.Id == pluginMeta.Id);
        if (assistantPlugin is null)
            return;

        var pluginHash = assistantPlugin.ComputeAuditHash();
        var cachedAudit = this.SettingsManager.ConfigurationData.AssistantPluginAudits.FirstOrDefault(x => x.PluginId == pluginMeta.Id);
        if (cachedAudit is not null && cachedAudit.PluginHash == pluginHash)
        {
            if (cachedAudit.Level < this.AssistantPluginAuditSettings.MinimumLevel && this.AssistantPluginAuditSettings.BlockActivationBelowMinimum)
            {
                await this.DialogService.ShowMessageBox(this.T("Assistant Audit"), $"{cachedAudit.Level.GetName()}: {cachedAudit.Summary}", this.T("Close"));
                return;
            }

            if (cachedAudit.Level < this.AssistantPluginAuditSettings.MinimumLevel &&
                !await this.ConfirmActivationBelowMinimumAsync(pluginMeta.Name, cachedAudit.Level))
            {
                return;
            }

            this.SettingsManager.ConfigurationData.EnabledPlugins.Add(pluginMeta.Id);
            await this.SettingsManager.StoreSettings();
            await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
            return;
        }

        var parameters = new DialogParameters<AssistantPluginAuditDialog>
        {
            { x => x.PluginId, pluginMeta.Id },
        };
        var dialog = await this.DialogService.ShowAsync<AssistantPluginAuditDialog>(this.T("Assistant Audit"), parameters, DialogOptions.FULLSCREEN);
        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not AssistantPluginAuditDialogResult auditResult)
            return;

        if (auditResult.Audit is not null)
            this.UpsertAuditCard(auditResult.Audit);

        if (auditResult.ActivatePlugin)
            this.SettingsManager.ConfigurationData.EnabledPlugins.Add(pluginMeta.Id);

        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task<bool> ConfirmActivationBelowMinimumAsync(string pluginName, AssistantAuditLevel actualLevel)
    {
        var dialogParameters = new DialogParameters<ConfirmDialog>
        {
            {
                x => x.Message,
                string.Format(
                    this.T("The assistant plugin \"{0}\" was audited with the level \"{1}\", which is below the required minimum level \"{2}\". Your current settings allow activation anyway, but this may be potentially dangerous. Do you really want to enable this plugin?"),
                    pluginName,
                    actualLevel.GetName(),
                    this.AssistantPluginAuditSettings.MinimumLevel.GetName())
            },
        };

        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(this.T("Potentially Dangerous Plugin"), dialogParameters,
            DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        return dialogResult is not null && !dialogResult.Canceled;
    }
    
    private static bool IsSendingMail(string sourceUrl) => sourceUrl.TrimStart().StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);

    private void UpsertAuditCard(PluginAssistantAudit audit)
    {
        var audits = this.SettingsManager.ConfigurationData.AssistantPluginAudits;
        var existingIndex = audits.FindIndex(x => x.PluginId == audit.PluginId);
        if (existingIndex >= 0)
            audits[existingIndex] = audit;
        else
            audits.Add(audit);
    }

    #region Overrides of MSGComponentBase

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.PLUGINS_RELOADED:
                await this.InvokeAsync(this.StateHasChanged);
                break;
        }
    }

    #endregion
}
