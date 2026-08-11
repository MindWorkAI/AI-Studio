using AIStudio.Assistants.BatchProcessing;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Dialogs.Settings;

public partial class SettingsDialogBatchProcessing : SettingsDialogBase
{
    private bool DefaultsDisabled() => !this.SettingsManager.ConfigurationData.BatchProcessing.PreselectOptions;

    private bool MinimumDelayIsManaged => ManagedConfiguration.TryGet(x => x.BatchProcessing, x => x.MinimumDelaySeconds, out var meta)
                                                  && meta.ManagedMode is not null;

    private int ManagedMinimumDelaySeconds => Math.Clamp(
        this.SettingsManager.ConfigurationData.BatchProcessing.MinimumDelaySeconds,
        DataBatchProcessing.MIN_DELAY_SECONDS,
        DataBatchProcessing.MAX_DELAY_SECONDS);

    private int EffectiveMinimumDelaySeconds => this.MinimumDelayIsManaged
        ? this.ManagedMinimumDelaySeconds
        : Math.Clamp(
            this.SettingsManager.ConfigurationData.BatchProcessing.MinimumDelaySeconds,
            DataBatchProcessing.MIN_DELAY_SECONDS,
            DataBatchProcessing.MAX_DELAY_SECONDS);

    private bool FreePromptImportDisabled() => this.DefaultsDisabled()
                                               || ManagedConfiguration.TryGet(x => x.BatchProcessing, x => x.FreePrompt, out var meta) && meta.IsLocked;

    private bool PromptFileImportDisabled() => this.DefaultsDisabled()
                                                || ManagedConfiguration.TryGet(x => x.BatchProcessing, x => x.PromptFilePath, out var meta) && meta.IsLocked;

    private async Task UpdateFreePromptFromFileAsync(string content)
    {
        this.SettingsManager.ConfigurationData.BatchProcessing.FreePrompt = content;
        await this.StoreImportedDefaultAsync();
    }

    private async Task UpdatePromptFilePathAsync(string path)
    {
        this.SettingsManager.ConfigurationData.BatchProcessing.PromptFilePath = path;
        await this.StoreImportedDefaultAsync();
    }

    private async Task StoreImportedDefaultAsync()
    {
        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

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

    private IReadOnlyList<ConfigurationSelectData<BatchProcessingCsvSeparator>> CsvSeparatorData =>
    [
        .. Enum
            .GetValues<BatchProcessingCsvSeparator>()
            .Select(value => new ConfigurationSelectData<BatchProcessingCsvSeparator>(value.Name(), value))
    ];

    private string? ValidateCustomCsvSeparator(string separator)
    {
        if (!BatchProcessingCsvSeparatorExtensions.IsValidCustomSeparator(separator))
            return T("Please enter exactly one punctuation or symbol character. Letters, numbers, spaces, quotation marks, and line breaks cannot be used as CSV separators.");

        return null;
    }

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
