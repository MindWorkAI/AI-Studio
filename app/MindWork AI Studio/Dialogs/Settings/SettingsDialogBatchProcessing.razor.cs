using AIStudio.Assistants.BatchProcessing;
using AIStudio.Settings;

namespace AIStudio.Dialogs.Settings;

public partial class SettingsDialogBatchProcessing : SettingsDialogBase
{
    private bool DefaultsDisabled() => !this.SettingsManager.ConfigurationData.BatchProcessing.PreselectOptions;

    private IReadOnlyList<ConfigurationSelectData<BatchProcessingPromptSource>> PromptSourceData =>
    [
        .. Enum
            .GetValues<BatchProcessingPromptSource>()
            .Select(value => new ConfigurationSelectData<BatchProcessingPromptSource>(value.Name(), value))
    ];

    private IReadOnlyList<ConfigurationSelectData<BatchProcessingOutputMode>> OutputModeData =>
    [
        .. Enum
            .GetValues<BatchProcessingOutputMode>()
            .Select(value => new ConfigurationSelectData<BatchProcessingOutputMode>(value.Name(), value))
    ];

    private IReadOnlyList<ConfigurationSelectData<string>> PolicyData
    {
        get
        {
            var selectedPolicyId = this.SettingsManager.ConfigurationData.BatchProcessing.PreselectedPolicyId;
            var policies = this.SettingsManager.ConfigurationData.DocumentAnalysis.Policies
                .Select(policy => new ConfigurationSelectData<string>(policy.PolicyName, policy.Id))
                .ToList();

            if (this.SelectedPolicyMissing)
                policies.Add(new(string.Format(T("Missing policy ({0})"), selectedPolicyId), selectedPolicyId));

            return policies;
        }
    }

    private bool SelectedPolicyMissing
    {
        get
        {
            var selectedPolicyId = this.SettingsManager.ConfigurationData.BatchProcessing.PreselectedPolicyId;
            return !string.IsNullOrWhiteSpace(selectedPolicyId) && this.SettingsManager.ConfigurationData.DocumentAnalysis.Policies.All(policy => policy.Id != selectedPolicyId);
        }
    }
}