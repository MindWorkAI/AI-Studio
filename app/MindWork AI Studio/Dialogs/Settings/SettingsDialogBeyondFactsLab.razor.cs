using AIStudio.Provider;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Dialogs.Settings;

/// <summary>
/// Provides the code-behind type for the settings of the BeyondFacts Lab.
/// </summary>
public partial class SettingsDialogBeyondFactsLab : SettingsDialogBase
{
    /// <summary>
    /// Whether the language of the app tells a formal address from an informal one, so that the form of address
    /// is a choice at all.
    /// </summary>
    private bool offersFormOfAddress;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        this.offersFormOfAddress = (await this.SettingsManager.GetActiveLanguagePlugin()).HasInformalAddress();
    }

    /// <summary>
    /// Shows the minimum confidence which applies, never one below the floor of the lab.
    /// </summary>
    /// <remarks>
    /// A value below the floor, e.g., from a configuration plugin, has no effect, and the list does not
    /// offer it either. Showing the floor instead tells what really counts.
    /// </remarks>
    private ConfidenceLevel SelectedMinimumConfidence() => Tools.Components.BEYOND_FACTS_LAB_ASSISTANT.MinimumConfidence(this.SettingsManager);
}