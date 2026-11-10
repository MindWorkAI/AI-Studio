using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs.Settings;

/// <summary>
/// Provides the code-behind type for the settings of the BeyondFacts Lab.
/// </summary>
public partial class SettingsDialogBeyondFactsLab : SettingsDialogBase
{
    [Inject]
    private BeyondFactsLabStore Store { get; init; } = null!;

    [Inject]
    private ILogger<SettingsDialogBeyondFactsLab> Logger { get; init; } = null!;

    /// <summary>
    /// Whether the language of the app tells a formal address from an informal one, so that the form of address
    /// is a choice at all.
    /// </summary>
    private bool offersFormOfAddress;

    /// <summary>
    /// Whether the settings for the further development of the lab are unfolded.
    /// </summary>
    private bool showExpertSettings;

    /// <summary>
    /// Whether the prompts are being copied into the test folder right now.
    /// </summary>
    private bool isCopyingPrompts;

    /// <summary>
    /// Gets whether an organization manages the prompt overrides.
    /// </summary>
    private static bool IsPromptOverridesLocked => ManagedConfiguration.TryGet(x => x.BeyondFactsLab, x => x.AllowPromptOverrides, out var meta) && meta.IsLocked;

    /// <summary>
    /// Gets whether an organization switched the prompt overrides off for good, so that the test folder serves no
    /// purpose.
    /// </summary>
    private bool ArePromptOverridesLockedOff => IsPromptOverridesLocked && !this.SettingsManager.ConfigurationData.BeyondFactsLab.AllowPromptOverrides;

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

    private void ToggleExpertSettings() => this.showExpertSettings = !this.showExpertSettings;

    /// <summary>
    /// Opens the test folder in the file manager, and creates it first when it is missing.
    /// </summary>
    private async Task OpenTestFolderAsync()
    {
        OpenPathResponse response;
        try
        {
            var directory = this.Store.PromptOverridesDirectory;
            Directory.CreateDirectory(directory);
            response = await this.RustService.TryOpenPathInRuntimeFileManager(directory);
        }
        catch (Exception exception)
        {
            this.Logger.LogWarning(exception, "Could not open the test folder of the BeyondFacts Lab.");
            await this.MessageBus.SendError(new(Icons.Material.Filled.FolderOpen, T("Could not open the test folder.")));
            return;
        }

        if (response.Success)
            return;

        var issue = string.IsNullOrWhiteSpace(response.Issue) ? T("Unknown error") : response.Issue;
        await this.MessageBus.SendError(new(Icons.Material.Filled.FolderOpen, string.Format(T("Could not open the test folder: {0}"), issue)));
    }

    /// <summary>
    /// Copies the prompts of this version into the test folder, so that changes start from them. A file which exists
    /// stays as it is.
    /// </summary>
    private async Task CopyPromptsAsync()
    {
        this.isCopyingPrompts = true;
        try
        {
            var copied = await BflPromptOverrides.CopyBuiltInAsync(this.Store.PromptOverridesDirectory);
            await this.MessageBus.SendSuccess(new(Icons.Material.Filled.ContentCopy, copied is 0
                ? T("The test folder holds every prompt already. AI Studio changed no file.")
                : string.Format(T("Files copied into the test folder: {0}. Files which were there already stay as they are."), copied)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.Logger.LogWarning(exception, "Could not copy the prompts of the BeyondFacts Lab into the test folder.");
            await this.MessageBus.SendError(new(Icons.Material.Filled.ContentCopy, T("Could not copy the prompts into the test folder.")));
        }
        finally
        {
            this.isCopyingPrompts = false;
        }
    }
}