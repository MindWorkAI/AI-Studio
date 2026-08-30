using AIStudio.Dialogs;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;
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
    /// <remarks>
    /// This is the one place which reports a failed load to the user, so callers neither have to
    /// repeat that nor may they treat a failure as an empty file.
    /// </remarks>
    /// <param name="filePath">The full path to the file to be read. Must not be null or empty.</param>
    /// <param name="rustService">Rust service used to read file content.</param>
    /// <param name="dialogService">Dialogservice used to display the Pandoc installation dialog if needed.</param>
    /// <param name="token">Cancels the extraction when the caller no longer needs the content.</param>
    /// <returns>The result of reading the file.</returns>
    public static async Task<FileExtractionResult> LoadFileData(string filePath, RustService rustService, IDialogService dialogService, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            LOGGER.LogError("Can't load from an empty or null file path.");
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Cancel, TB("The file path is null or empty and the file therefore can not be loaded.")));
            return FileExtractionResult.Failed(FileExtractionErrorCode.INVALID_REQUEST, "The file path is null or empty.");
        }

        var fileName = Path.GetFileName(filePath);

        //
        // Ensure that Pandoc is installed and ready. This is only needed for the formats we
        // convert with it: PDFs and the other document types are read by the Rust runtime itself.
        //
        if (FileTypes.RequiresPandoc(filePath))
        {
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
                    LOGGER.LogError("Pandoc is not available after installation attempt, so '{FilePath}' cannot be read.", filePath);
                    await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Cancel, FileExtractionErrorCode.PANDOC_UNAVAILABLE.ToUserMessage(fileName)));
                    return FileExtractionResult.Failed(FileExtractionErrorCode.PANDOC_UNAVAILABLE, "Pandoc is required to read this file, but it is not available.");
                }
            }
        }

        var result = await rustService.ReadArbitraryFileData(filePath, int.MaxValue, token: token);

        //
        // Nobody wants to read that their own cancellation failed. We hand the result back so the
        // caller can tell the two apart, but we report nothing to the user:
        //
        if (result.ErrorCode is FileExtractionErrorCode.CANCELLED)
            return result;

        if (!result.HasUsableContent)
        {
            LOGGER.LogError("Reading the file '{FilePath}' failed: code={ErrorCode}, message='{ErrorMessage}'.", filePath, result.ErrorCode, result.ErrorMessage);
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Description, result.ToUserMessage(fileName)));
        }
        else if (result.Outcome is FileExtractionOutcome.PARTIAL)
        {
            LOGGER.LogWarning("Parts of the file '{FilePath}' could not be read: pages={FailedPages}.", filePath, string.Join(", ", result.FailedPages));
            await MessageBus.INSTANCE.SendWarning(new(Icons.Material.Filled.Description, result.ToPartialUserMessage(fileName)));
        }

        // The file was read correctly, but its extension lies about what it contains:
        if (result.HasExtensionMismatch)
        {
            LOGGER.LogWarning("The file '{FilePath}' is actually a '{DetectedFormat}'.", filePath, result.DetectedFormat);
            await MessageBus.INSTANCE.SendWarning(new(Icons.Material.Filled.RuleFolder, result.ToExtensionMismatchUserMessage(fileName)));
        }

        return result;
    }
}