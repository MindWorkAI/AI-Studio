using AIStudio.Provider;

namespace AIStudio.Dialogs.Settings;

/// <summary>
/// Provides the code-behind type for the settings of the BeyondFacts Lab.
/// </summary>
public partial class SettingsDialogBeyondFactsLab : SettingsDialogBase
{
    /// <summary>
    /// Shows the minimum confidence which applies, never one below the floor of the lab.
    /// </summary>
    /// <remarks>
    /// A value below the floor, e.g., from a configuration plugin, has no effect, and the list does not
    /// offer it either. Showing the floor instead tells what really counts.
    /// </remarks>
    private ConfidenceLevel SelectedMinimumConfidence() => Tools.Components.BEYOND_FACTS_LAB_ASSISTANT.MinimumConfidence(this.SettingsManager);
}