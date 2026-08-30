using System.Diagnostics;
using AIStudio.Chat;
using AIStudio.Dialogs;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;
using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Tools;

public static class PandocExport
{
    private static readonly ILogger LOGGER = Program.LOGGER_FACTORY.CreateLogger(nameof(PandocExport));

    private static string TB(string fallbackEn) => I18N.I.T(fallbackEn, typeof(PandocExport).Namespace, nameof(PandocExport));

    /// <summary>
    /// Converts the given content to a document using Pandoc and lets the user save it.
    /// </summary>
    /// <param name="rustService">The Rust service, used for the save dialog and for Pandoc.</param>
    /// <param name="dialogService">The dialog service, used to offer the Pandoc installation.</param>
    /// <param name="dialogTitle">The title of the save dialog. The caller knows what the user is
    /// looking at, a chat message or the result of an assistant, so the caller names it.</param>
    /// <param name="format">The format to write. Must be a format which uses Pandoc.</param>
    /// <param name="markdownContent">The content to export.</param>
    /// <returns>True, when the document was written.</returns>
    public static async Task<bool> ToDocument(RustService rustService, IDialogService dialogService, string dialogTitle, FileExportFormat format, IContent markdownContent)
    {
        if (!format.UsesPandoc() || format.ToFileTypeFilter() is not { } fileTypeFilter)
            throw new ArgumentOutOfRangeException(nameof(format), format, "Pandoc cannot write this format.");

        //
        // We read the text before we ask for a path: when there is nothing to convert, the user
        // should learn that right away instead of picking a file first and getting an error afterwards.
        //
        if (!markdownContent.TryGetMarkdownText(out var markdownText))
        {
            LOGGER.LogWarning("Cannot export the content as {ExportFormat}, because it carries no text.", format);
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Cancel, TB("Only text messages can be exported.")));
            return false;
        }

        var response = await rustService.SaveFile(dialogTitle, [fileTypeFilter], format.ToSuggestedFileName());
        if (response.UserCancelled)
        {
            LOGGER.LogInformation("User cancelled the save dialog.");
            return false;
        }

        LOGGER.LogInformation("The user chose the path '{SaveFilePath}' for the {ExportFormat} export.", response.SaveFilePath, format);

        var tempMarkdownFilePath = string.Empty;
        try
        {
            var tempMarkdownFile = Guid.NewGuid().ToString();
            tempMarkdownFilePath = Path.Combine(Path.GetTempPath(), tempMarkdownFile);

            // Write text content to a temporary file:
            await File.WriteAllTextAsync(tempMarkdownFilePath, markdownText);

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
                    await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Cancel, TB("Pandoc is required for this export.")));
                    return false;
                }
            }

            // Call Pandoc to create the document:
            var pandoc = await PandocProcessBuilder
                .Create()
                .UseStandaloneMode()
                .WithInputFormat("gfm+emoji+tex_math_dollars")
                .WithOutputFormat(format.ToPandocOutputFormat())
                .WithOutputFile(response.SaveFilePath)
                .WithInputFile(tempMarkdownFilePath)
                .BuildAsync(rustService);

            using var process = Process.Start(pandoc.StartInfo);
            if (process is null)
            {
                LOGGER.LogError("Failed to start Pandoc process.");
                return false;
            }

            // Read output streams asynchronously while the process runs (prevents deadlock):
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            // Wait for the process to exit AND for streams to be fully read:
            await process.WaitForExitAsync();
            await outputTask;
            var error = await errorTask;

            if (process.ExitCode is not 0)
            {
                LOGGER.LogError("Pandoc failed with exit code {ProcessExitCode}: '{ErrorText}'", process.ExitCode, error);
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Cancel, TB("The export failed.")));
                return false;
            }

            LOGGER.LogInformation("Pandoc conversion successful.");
            await MessageBus.INSTANCE.SendSuccess(new(Icons.Material.Filled.CheckCircle, TB("The export succeeded.")));
            
            return true;
        }
        catch (Exception ex)
        {
            LOGGER.LogError(ex, "Error during {ExportFormat} export.", format);
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Cancel, TB("The export failed.")));
            return false;
        }
        finally
        {
            // Try to remove the temp file:
            if (!string.IsNullOrWhiteSpace(tempMarkdownFilePath))
            {
                try
                {
                    File.Delete(tempMarkdownFilePath);
                }
                catch
                {
                    LOGGER.LogWarning($"Was not able to delete temporary file: '{tempMarkdownFilePath}'");
                }
            }
        }
    }
}
