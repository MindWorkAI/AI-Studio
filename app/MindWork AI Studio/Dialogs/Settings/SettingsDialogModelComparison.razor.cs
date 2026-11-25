using AIStudio.Assistants.ModelComparison;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs.Settings;

public partial class SettingsDialogModelComparison : SettingsDialogBase
{
    [Inject]
    private ModelComparisonStore ModelComparisonStore { get; init; } = null!;

    [Inject]
    private ILogger<SettingsDialogModelComparison> Logger { get; init; } = null!;

    /// <summary>
    /// The presets of the mode Answer Variance, as the store listed them when the dialog opened.
    /// </summary>
    private IReadOnlyList<ModelComparisonPreset> presets = [];

    /// <summary>
    /// Whether the presets were listed. Until then, a preselected preset does not count as missing.
    /// </summary>
    private bool presetsListed;

    private bool OptionsDisabled() => !this.SettingsManager.ConfigurationData.ModelComparison.PreselectOptions;

    #region Overrides of SettingsDialogBase

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        try
        {
            this.presets = await this.ModelComparisonStore.ListPresetsAsync();
            this.presetsListed = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.Logger.LogWarning(exception, "Could not list the presets of the Model Comparison assistant.");
        }
    }

    #endregion

    /// <summary>
    /// Gets the entries of the preset selection: none, every preset, and the preselected one when it no longer exists.
    /// </summary>
    private IReadOnlyList<ConfigurationSelectData<string>> PresetData
    {
        get
        {
            var selectedPresetId = this.SettingsManager.ConfigurationData.ModelComparison.PreselectedPresetId;
            List<ConfigurationSelectData<string>> entries =
            [
                new(T("No preset preselected"), string.Empty),
                .. this.presets.Select(preset => new ConfigurationSelectData<string>(preset.Name, preset.Id.ToString("D")))
            ];

            if (this.SelectedPresetMissing)
                entries.Add(new(string.Format(T("Missing preset ({0})"), selectedPresetId), selectedPresetId));

            return entries;
        }
    }

    /// <summary>
    /// Gets whether the preselected preset was deleted since.
    /// </summary>
    private bool SelectedPresetMissing
    {
        get
        {
            var selectedPresetId = this.SettingsManager.ConfigurationData.ModelComparison.PreselectedPresetId;
            return this.presetsListed
                   && !string.IsNullOrWhiteSpace(selectedPresetId)
                   && this.presets.All(preset => !string.Equals(preset.Id.ToString("D"), selectedPresetId, StringComparison.OrdinalIgnoreCase));
        }
    }
}