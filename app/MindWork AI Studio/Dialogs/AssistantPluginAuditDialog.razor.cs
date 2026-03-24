using AIStudio.Agents.AssistantAudit;
using AIStudio.Components;
using AIStudio.Provider;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class AssistantPluginAuditDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Inject]
    private AssistantAuditAgent AuditAgent { get; init; } = null!;

    [Parameter]
    public Guid PluginId { get; set; }

    private PluginAssistants? plugin;
    private PluginAssistantAudit? audit;
    private string promptPreview = string.Empty;
    private string componentSummary = string.Empty;
    private string luaCode = string.Empty;
    private bool isAuditing;

    private AIStudio.Settings.Provider CurrentProvider => this.SettingsManager.GetPreselectedProvider(Tools.Components.AGENT_ASSISTANT_PLUGIN_AUDIT, null, true);
    private string ProviderLabel => this.CurrentProvider == AIStudio.Settings.Provider.NONE
        ? this.T("No provider configured")
        : $"{this.CurrentProvider.InstanceName} ({this.CurrentProvider.UsedLLMProvider.ToName()})";
    private AssistantAuditLevel MinimumLevel => this.SettingsManager.ConfigurationData.AssistantPluginAudit.MinimumLevel;
    private string MinimumLevelLabel => this.MinimumLevel.GetName();
    private bool CanRunAudit => this.plugin is not null && this.CurrentProvider != AIStudio.Settings.Provider.NONE && !this.isAuditing;
    private bool CanEnablePlugin => this.audit is not null && (this.audit.Level >= this.MinimumLevel || !this.SettingsManager.ConfigurationData.AssistantPluginAudit.BlockActivationBelowMinimum);
    private Color EnableButtonColor => this.audit is not null && this.audit.Level >= this.MinimumLevel ? Color.Success : Color.Warning;

    protected override async Task OnInitializedAsync()
    {
        this.plugin = PluginFactory.RunningPlugins.OfType<PluginAssistants>().FirstOrDefault(x => x.Id == this.PluginId);
        if (this.plugin is not null)
        {
            this.promptPreview = await this.plugin.BuildAuditPromptPreviewAsync();
            this.componentSummary = this.plugin.CreateAuditComponentSummary();
            this.luaCode = this.plugin.ReadManifestCode();
        }

        await base.OnInitializedAsync();
    }

    private async Task RunAudit()
    {
        if (this.plugin is null || this.isAuditing)
            return;

        this.isAuditing = true;
        await this.InvokeAsync(this.StateHasChanged);

        try
        {
            var result = await this.AuditAgent.AuditAsync(this.plugin);
            this.audit = new PluginAssistantAudit
            {
                PluginId = this.plugin.Id,
                PluginHash = this.plugin.ComputeAuditHash(),
                AuditedAtUtc = DateTimeOffset.UtcNow,
                AuditProviderId = this.CurrentProvider.Id,
                AuditProviderName = this.CurrentProvider == AIStudio.Settings.Provider.NONE ? string.Empty : this.CurrentProvider.InstanceName,
                Level = AssistantAuditLevelExtensions.Parse(result.Level),
                Summary = result.Summary,
                Confidence = result.Confidence,
                PromptPreview = this.promptPreview,
                Findings = result.Findings,
            };
        }
        finally
        {
            this.isAuditing = false;
            await this.InvokeAsync(this.StateHasChanged);
        }
    }

    private void CloseWithoutActivation()
    {
        if (this.audit is null)
        {
            this.MudDialog.Cancel();
            return;
        }

        this.MudDialog.Close(DialogResult.Ok(new AssistantPluginAuditDialogResult(this.audit, false)));
    }

    private void EnablePlugin()
    {
        if (this.audit is null)
            return;

        this.MudDialog.Close(DialogResult.Ok(new AssistantPluginAuditDialogResult(this.audit, true)));
    }
}
