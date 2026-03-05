using AIStudio.Provider;
using AIStudio.Tools.PluginSystem;

using Lua;

namespace AIStudio.Settings.DataModel;

public sealed record DataDocumentAnalysisPolicy : ConfigurationBaseObject
{
    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger<DataDocumentAnalysisPolicy>();

    /// <inheritdoc />
    public override string Id { get; init; } = string.Empty;
    
    /// <inheritdoc />
    public override uint Num { get; init; }
    
    /// <inheritdoc />
    public override string Name
    {
        get => this.PolicyName;
        init => this.PolicyName = value;
    }
    
    /// <inheritdoc />
    public override bool IsEnterpriseConfiguration { get; init; }
    
    /// <summary>
    /// The name of the document analysis policy.
    /// </summary>
    public string PolicyName { get; set; } = string.Empty;
    
    /// <summary>
    /// The description of the document analysis policy.
    /// </summary>
    public string PolicyDescription { get; set; } = string.Empty;

    /// <summary>
    /// Is this policy protected? If so, it cannot be deleted or modified by the user.
    /// </summary>
    public bool IsProtected { get; set; }
    
    /// <inheritdoc />
    public override Guid EnterpriseConfigurationPluginId { get; init; } = Guid.Empty;

    /// <summary>
    /// The rules for the document analysis policy.
    /// </summary>
    public string AnalysisRules { get; set; } = string.Empty;

    /// <summary>
    /// The rules for the output of the document analysis, e.g., the desired format, structure, etc.
    /// </summary>
    public string OutputRules { get; set; } = string.Empty;
    
    /// <summary>
    /// The minimum confidence level required for a provider to be considered.
    /// </summary>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ConfidenceLevel.NONE;
    
    /// <summary>
    /// Which LLM provider should be preselected?
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;
    
    /// <summary>
    /// Preselect a profile?
    /// </summary>
    public string PreselectedProfile { get; set; } = string.Empty;

    /// <summary>
    /// Hide the policy definition section in the UI?
    /// If true, the policy definition panel will be hidden and only the document selection will be shown.
    /// This is useful for enterprise configurations where users should not see or modify the policy details.
    /// </summary>
    public bool HidePolicyDefinition { get; set; }

    public static bool TryProcessConfiguration(int idx, LuaTable table, Guid configPluginId, out ConfigurationBaseObject policy)
    {
        policy = new DataDocumentAnalysisPolicy();
        if (!table.TryGetValue("Id", out var idValue) || !idValue.TryRead<string>(out var idText) || !Guid.TryParse(idText, out var id))
        {
            LOG.LogWarning("The configured document analysis policy {PolicyIndex} does not contain a valid ID. The ID must be a valid GUID.", idx);
            return false;
        }
        
        if (!table.TryGetValue("PolicyName", out var nameValue) || !nameValue.TryRead<string>(out var name) || string.IsNullOrWhiteSpace(name))
        {
            LOG.LogWarning("The configured document analysis policy {PolicyIndex} does not contain a valid PolicyName field.", idx);
            return false;
        }
        
        if (!table.TryGetValue("PolicyDescription", out var descriptionValue) || !descriptionValue.TryRead<string>(out var description) || string.IsNullOrWhiteSpace(description))
        {
            LOG.LogWarning("The configured document analysis policy {PolicyIndex} does not contain a valid PolicyDescription field.", idx);
            return false;
        }
        
        if (!table.TryGetValue("AnalysisRules", out var analysisRulesValue) || !analysisRulesValue.TryRead<string>(out var analysisRules) || string.IsNullOrWhiteSpace(analysisRules))
        {
            LOG.LogWarning("The configured document analysis policy {PolicyIndex} does not contain valid AnalysisRules field.", idx);
            return false;
        }
        
        if (!table.TryGetValue("OutputRules", out var outputRulesValue) || !outputRulesValue.TryRead<string>(out var outputRules) || string.IsNullOrWhiteSpace(outputRules))
        {
            LOG.LogWarning("The configured document analysis policy {PolicyIndex} does not contain valid OutputRules field.", idx);
            return false;
        }

        var minimumConfidence = ConfidenceLevel.NONE;
        if (table.TryGetValue("MinimumProviderConfidence", out var minConfValue) && minConfValue.TryRead<string>(out var minConfText))
        {
            if (!Enum.TryParse(minConfText, true, out minimumConfidence))
            {
                LOG.LogWarning("The configured document analysis policy {PolicyIndex} contains an invalid MinimumProviderConfidence: {ConfidenceLevel}.", idx, minConfText);
                minimumConfidence = ConfidenceLevel.NONE;
            }
        }
        
        var preselectedProvider = string.Empty;
        if (table.TryGetValue("PreselectedProvider", out var providerValue) && providerValue.TryRead<string>(out var providerId))
            preselectedProvider = providerId;

        var preselectedProfile = string.Empty;
        if (table.TryGetValue("PreselectedProfile", out var profileValue) && profileValue.TryRead<string>(out var profileId))
            preselectedProfile = profileId;

        var hidePolicyDefinition = false;
        if (table.TryGetValue("HidePolicyDefinition", out var hideValue) && hideValue.TryRead<bool>(out var hide))
            hidePolicyDefinition = hide;

        policy = new DataDocumentAnalysisPolicy
        {
            Id = id.ToString(),
            Num = 0, // will be set later by the PluginConfigurationObject
            PolicyName = name,
            PolicyDescription = description,
            AnalysisRules = analysisRules,
            OutputRules = outputRules,
            MinimumProviderConfidence = minimumConfidence,
            PreselectedProvider = preselectedProvider,
            PreselectedProfile = preselectedProfile,
            HidePolicyDefinition = hidePolicyDefinition,
            IsProtected = true,
            IsEnterpriseConfiguration = true,
            EnterpriseConfigurationPluginId = configPluginId,
        };
        
        return true;
    }
}