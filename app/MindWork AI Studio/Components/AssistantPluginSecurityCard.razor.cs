using System.Globalization;
using AIStudio.Dialogs;
using AIStudio.Tools.PluginSystem.Assistants;
using Microsoft.AspNetCore.Components;
using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

public partial class AssistantPluginSecurityCard : MSGComponentBase
{
    [Parameter]
    public PluginAssistants? Plugin { get; set; }

    [Parameter]
    public bool Compact { get; set; }

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    private PluginAssistantSecurityState SecurityState => this.Plugin is null
        ? new PluginAssistantSecurityState()
        : PluginAssistantSecurityResolver.Resolve(this.SettingsManager, this.Plugin);

    private CultureInfo currentCultureInfo = CultureInfo.InvariantCulture;
    private bool showSecurityCard;
    private bool showDetails;
    private bool showMetadata;

    protected override async Task OnInitializedAsync()
    {
        var activeLanguagePlugin = await this.SettingsManager.GetActiveLanguagePlugin();
        this.currentCultureInfo = CommonTools.DeriveActiveCultureOrInvariant(activeLanguagePlugin.IETFTag);
        this.showDetails = !this.Compact;
        this.showMetadata = false;
        
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED, Event.PLUGINS_RELOADED ]);
        await base.OnInitializedAsync();
    }

    private async Task OpenAuditDialogAsync()
    {
        if (this.Plugin is null)
            return;

        var parameters = new DialogParameters<AssistantPluginAuditDialog>
        {
            { x => x.PluginId, this.Plugin.Id },
        };
        var dialog = await this.DialogService.ShowAsync<AssistantPluginAuditDialog>(this.T("Assistant Audit"), parameters, DialogOptions.FULLSCREEN);
        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not AssistantPluginAuditDialogResult auditResult)
            return;

        if (auditResult.Audit is not null)
            UpsertAudit(this.SettingsManager.ConfigurationData.AssistantPluginAudits, auditResult.Audit);

        if (auditResult.ActivatePlugin && !this.SettingsManager.ConfigurationData.EnabledPlugins.Contains(this.Plugin.Id))
            this.SettingsManager.ConfigurationData.EnabledPlugins.Add(this.Plugin.Id);

        await this.SettingsManager.StoreSettings();
        await this.SendMessage(Event.CONFIGURATION_CHANGED, true);
    }

    protected override Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        if (triggeredEvent is Event.CONFIGURATION_CHANGED or Event.PLUGINS_RELOADED)
            return this.InvokeAsync(this.StateHasChanged);

        return Task.CompletedTask;
    }
    
    private void ToggleSecurityCard() => this.showSecurityCard = !this.showSecurityCard;

    private void HideSecurityCard() => this.showSecurityCard = false;

    private void ToggleDetails() => this.showDetails = !this.showDetails;

    private void ToggleMetadata() => this.showMetadata = !this.showMetadata;

    private static void UpsertAudit(List<PluginAssistantAudit> audits, PluginAssistantAudit audit)
    {
        var existingIndex = audits.FindIndex(x => x.PluginId == audit.PluginId);
        if (existingIndex >= 0)
            audits[existingIndex] = audit;
        else
            audits.Add(audit);
    }
    
    private string FormatFileTimestamp(DateTime timestamp) => CommonTools.FormatTimestampToGeneral(timestamp, this.currentCultureInfo);

    private string GetPopoverStyle() => $"border-color: {this.GetStatusBorderColor()};";

    private double GetConfidencePercentage()
    {
        var confidence = this.SecurityState.Audit?.Confidence ?? 0f;
        if (confidence <= 1)
            confidence *= 100;

        return Math.Clamp(confidence, 0, 100);
    }

    private string GetConfidenceLabel() => $"{this.GetConfidencePercentage():0}%";

    private string GetFindingSummary()
    {
        var count = this.SecurityState.Audit?.Findings.Count ?? 0;
        return string.Format(this.T("{0} Finding(s)"), count);
    }

    private string GetAuditTimestampLabel()
    {
        var auditedAt = this.SecurityState.Audit?.AuditedAtUtc;
        return auditedAt is null
            ? this.T("No audit yet")
            : this.FormatFileTimestamp(auditedAt.Value.ToLocalTime().DateTime);
    }

    private string GetAuditProviderLabel()
    {
        var providerName = this.SecurityState.Audit?.AuditProviderName;
        return string.IsNullOrWhiteSpace(providerName) ? this.T("Unknown") : providerName;
    }

    private static string GetShortHash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash) || hash.Length <= 16)
            return hash;

        return $"{hash[..8]}...{hash[^8..]}";
    }

    private Severity GetStatusSeverity() => this.SecurityState.AuditColor switch
    {
        Color.Success => Severity.Success,
        Color.Warning => Severity.Warning,
        Color.Error => Severity.Error,
        _ => Severity.Info,
    };

    private string GetStatusBorderColor() => this.SecurityState.AuditColor switch
    {
        Color.Success => "var(--mud-palette-success)",
        Color.Warning => "var(--mud-palette-warning)",
        Color.Error => "var(--mud-palette-error)",
        _ => "var(--mud-palette-info)",
    };
}
