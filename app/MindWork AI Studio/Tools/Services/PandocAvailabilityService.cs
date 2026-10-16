using AIStudio.Dialogs;
using AIStudio.Tools.PluginSystem;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Tools.Services;

/// <summary>
/// Service to check Pandoc availability and ensure installation.
/// This service encapsulates the logic for checking if Pandoc is installed
/// and showing the installation dialog if needed.
/// </summary>
public sealed class PandocAvailabilityService(RustService rustService, IDialogService dialogService, ILogger<PandocAvailabilityService> logger)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PandocAvailabilityService).Namespace, nameof(PandocAvailabilityService));

    private RustService RustService => rustService;
    
    private IDialogService DialogService => dialogService;
    
    private ILogger<PandocAvailabilityService> Logger => logger;

    private PandocInstallation? cachedInstallation;

    /// <summary>
    /// Checks if Pandoc is available and shows the installation dialog if needed.
    /// </summary>
    /// <param name="showSuccessMessage">Whether to show a success message if Pandoc is available.</param>
    /// <param name="showDialog">Whether to show the installation dialog if Pandoc is not available.</param>
    /// <param name="showErrorMessage">Whether to report a still missing Pandoc to the user. Turn
    /// this off when you can say it better yourself, for example by naming the file which cannot
    /// be read; otherwise the user reads two messages about the same thing.</param>
    /// <returns>The Pandoc installation state.</returns>
    public async Task<PandocInstallation> EnsureAvailabilityAsync(bool showSuccessMessage = false, bool showDialog = true, bool showErrorMessage = true)
    {
        // Check if Pandoc is available:
        var pandocState = await Pandoc.CheckAvailabilityAsync(this.RustService, showMessages: false, showSuccessMessage: showSuccessMessage);

        // Cache the result:
        this.cachedInstallation = pandocState;

        // If not available, show installation dialog:
        if (!pandocState.IsAvailable && showDialog)
        {
            var dialogParameters = new DialogParameters<PandocDialog>
            {
                { x => x.ShowInitialResultInSnackbar, false },
            };

            var dialogReference = await this.DialogService.ShowAsync<PandocDialog>(TB("Pandoc Installation"), dialogParameters, DialogOptions.FULLSCREEN);
            await dialogReference.Result;

            // Re-check availability after dialog:
            pandocState = await Pandoc.CheckAvailabilityAsync(this.RustService, showMessages: showSuccessMessage, showSuccessMessage: showSuccessMessage);
            this.cachedInstallation = pandocState;

            if (!pandocState.IsAvailable)
            {
                this.Logger.LogError("Pandoc is not available after installation attempt.");
                if (showErrorMessage)
                    await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Cancel, TB("AI Studio needs Pandoc for this, but it is not available.")));
            }
        }

        return pandocState;
    }

    /// <summary>
    /// Checks if Pandoc is available without showing any dialogs or messages.
    /// Uses cached result if available to avoid redundant checks.
    /// </summary>
    /// <returns>True if Pandoc is available, false otherwise.</returns>
    public async Task<bool> IsAvailableAsync()
    {
        if (this.cachedInstallation.HasValue)
            return this.cachedInstallation.Value.IsAvailable;

        var pandocState = await Pandoc.CheckAvailabilityAsync(this.RustService, showMessages: false, showSuccessMessage: false);
        this.cachedInstallation = pandocState;

        return pandocState.IsAvailable;
    }

    /// <summary>
    /// Clears the cached Pandoc installation state.
    /// Useful when the installation state might have changed.
    /// </summary>
    public void ClearCache() => this.cachedInstallation = null;
}
