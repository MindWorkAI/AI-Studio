using AIStudio.Components;
using AIStudio.Agents.AssistantAudit;
using AIStudio.Dialogs;
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

        if (pluginMeta.Type is not PluginType.ASSISTANT || !this.SettingsManager.ConfigurationData.AssistantPluginAudit.RequireAuditBeforeActivation)
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
            if (cachedAudit.Level < this.SettingsManager.ConfigurationData.AssistantPluginAudit.MinimumLevel && this.SettingsManager.ConfigurationData.AssistantPluginAudit.BlockActivationBelowMinimum)
            {
                await this.DialogService.ShowMessageBox(this.T("Assistant Audit"), $"{cachedAudit.Level.GetName()}: {cachedAudit.Summary}", this.T("Close"));
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
