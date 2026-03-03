using AIStudio.Dialogs;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;
using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Tools;

public static class UserFile
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(UserFile).Namespace, nameof(UserFile));

    private static readonly ILogger LOGGER = Program.LOGGER_FACTORY.CreateLogger(nameof(UserFile));
    
    /// <summary>
    /// Attempts to load the content of a file at the specified path, ensuring Pandoc is installed and available before proceeding.
    /// </summary>
    /// <param name="filePath">The full path to the file to be read. Must not be null or empty.</param>
    /// <param name="rustService">Rust service used to read file content.</param>
    /// <param name="dialogService">Dialogservice used to display the Pandoc installation dialog if needed.</param>
    public static async Task<string> LoadFileData(string filePath, RustService rustService, IDialogService dialogService)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            LOGGER.LogError("Can't load from an empty or null file path.");
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Cancel, TB("The file path is null or empty and the file therefore can not be loaded.")));
        }
        
        // Ensure that Pandoc is installed and ready:
        var pandocState = await Pandoc.CheckAvailabilityAsync(rustService, showSuccessMessage: false);
        if (!pandocState.IsAvailable)
        {
            var dialogParameters = new DialogParameters<PandocDialog>
            {
                { x => x.ShowInitialResultInSnackbar, false },
            };
                
            var dialogReference = await dialogService.ShowAsync<PandocDialog>(TB("Pandoc Installation"), dialogParameters, DialogOptions.FULLSCREEN);
            await dialogReference.Result;
                
            pandocState = await Pandoc.CheckAvailabilityAsync(rustService, showSuccessMessage: true);
            if (!pandocState.IsAvailable)
            {
                LOGGER.LogError("Pandoc is not available after installation attempt.");
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Cancel, TB("Pandoc may be required for importing files.")));
            }
        }
        
        var fileContent = await rustService.ReadArbitraryFileData(filePath, int.MaxValue);
        return fileContent;
    }
}