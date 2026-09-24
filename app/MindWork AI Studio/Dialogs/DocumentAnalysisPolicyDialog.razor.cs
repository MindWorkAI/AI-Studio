using AIStudio.Components;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.ToolCallingSystem;

using Lua;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class DocumentAnalysisPolicyDialog : MSGComponentBase
{
    [Parameter]
    public LuaTable? ImportedConfiguration { get; set; }

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Inject]
    private ToolRegistry ToolRegistry { get; init; } = null!;

    private MudForm form = null!;
    private bool isValid;
    private string[] issues = [];
    private string referenceIssue = string.Empty;
    private string name = string.Empty;
    private string description = string.Empty;
    private string analysisRules = string.Empty;
    private string outputRules = string.Empty;
    private ConfidenceLevel minimumConfidence = ConfidenceLevel.NONE;
    private HashSet<string> allowedToolIds = new(StringComparer.Ordinal);
    private string providerId = string.Empty;
    private string profileId = Profile.NO_PROFILE.Id;
    private bool hideDefinition;
    private bool isProtected;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && this.ImportedConfiguration is not null)
        {
            if (!this.SettingsManager.ConfigurationData.App.CanImportConfigurationSnippet("DOCUMENT_ANALYSIS_POLICIES"))
                this.MudDialog.Cancel();
            else
                await this.ImportConfiguration(this.ImportedConfiguration);
            this.StateHasChanged();
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    private async Task ImportConfiguration(LuaTable table)
    {
        ConfigurationImportFields.ValidateExportId(table);
        var importedName = ConfigurationImportFields.String(table, "PolicyName");
        var importedDescription = ConfigurationImportFields.String(table, "PolicyDescription");
        var importedAnalysisRules = ConfigurationImportFields.String(table, "AnalysisRules");
        var importedOutputRules = ConfigurationImportFields.String(table, "OutputRules");
        var confidence = ConfigurationImportFields.Enum<ConfidenceLevel>(table, "MinimumProviderConfidence");
        var toolIds = ConfigurationImportFields.Strings(table, "AllowedToolIds");
        var importedProviderId = ConfigurationImportFields.String(table, "PreselectedProvider", required: false);
        var importedProfileId = ConfigurationImportFields.String(table, "PreselectedProfile", required: false);
        var hide = ConfigurationImportFields.Bool(table, "HidePolicyDefinition");

        this.name = importedName;
        this.description = importedDescription;
        this.analysisRules = importedAnalysisRules;
        this.outputRules = importedOutputRules;
        this.minimumConfidence = confidence;
        this.allowedToolIds = new(toolIds, StringComparer.Ordinal);
        this.providerId = importedProviderId;
        this.profileId = importedProfileId;
        this.hideDefinition = hide;

        var missing = new List<string>();
        if (!string.IsNullOrWhiteSpace(this.providerId) && this.SettingsManager.GetAllProviders().All(provider => provider.Id != this.providerId))
            missing.Add($"provider {this.providerId}");
        if (!string.IsNullOrWhiteSpace(this.profileId) && this.profileId != Profile.NO_PROFILE.Id && this.SettingsManager.ConfigurationData.Profiles.All(profile => profile.Id != this.profileId))
            missing.Add($"profile {this.profileId}");
        var availableToolIds = (await this.ToolRegistry.GetCatalogAsync(AIStudio.Tools.Components.DOCUMENT_ANALYSIS_ASSISTANT))
            .Select(item => item.Definition.Id).ToHashSet(StringComparer.Ordinal);
        missing.AddRange(this.allowedToolIds.Where(id => !availableToolIds.Contains(id)).Select(id => $"tool {id}"));
        this.referenceIssue = missing.Count == 0 ? string.Empty : $"Unavailable references: {string.Join(", ", missing)}. Review the selections before saving.";
        this.form.ResetValidation();
    }

    private async Task Store()
    {
        if (this.ImportedConfiguration is not null && !this.SettingsManager.ConfigurationData.App.CanImportConfigurationSnippet("DOCUMENT_ANALYSIS_POLICIES"))
            return;

        await this.form.Validate();
        if (!this.isValid)
            return;

        this.MudDialog.Close(DialogResult.Ok(new DataDocumentAnalysisPolicy
        {
            Id = Guid.NewGuid().ToString(),
            PolicyName = this.name.Trim(),
            PolicyDescription = this.description.Trim(),
            AnalysisRules = this.analysisRules.Trim(),
            OutputRules = this.outputRules.Trim(),
            MinimumProviderConfidence = this.minimumConfidence,
            AllowedToolIds = new(this.allowedToolIds, StringComparer.Ordinal),
            PreselectedProvider = this.providerId,
            PreselectedProfile = this.profileId,
            HidePolicyDefinition = this.hideDefinition,
            IsProtected = this.isProtected,
        }));
    }

    private string? ValidateName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return T("Please provide a name for your policy. This name will be used to identify the policy in AI Studio.");
        if (value.Length is < 6 or > 60)
            return T("The name of your policy must be between 6 and 60 characters long.");
        if (this.SettingsManager.ConfigurationData.DocumentAnalysis.Policies.Any(policy => policy.PolicyName == value))
            return T("A policy with this name already exists. Please choose a different name.");
        return null;
    }

    private string? ValidateDescription(string value) => value.Length is < 32 or > 512
        ? T("The description of your policy must be between 32 and 512 characters long.") : null;

    private string? ValidateAnalysisRules(string value) => string.IsNullOrWhiteSpace(value)
        ? T("Please provide a description of your analysis rules. This rules will be used to instruct the AI on how to analyze the documents.") : null;

    private string? ValidateOutputRules(string value) => string.IsNullOrWhiteSpace(value)
        ? T("Please provide a description of your output rules. This rules will be used to instruct the AI on how to format the output of the analysis.") : null;

    private void Cancel() => this.MudDialog.Cancel();
}
