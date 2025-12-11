using System.Diagnostics;
using AIStudio.Chat;
using AIStudio.Dialogs;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Tools;

public static class PandocExport
{
    private static readonly ILogger LOGGER = Program.LOGGER_FACTORY.CreateLogger(nameof(PandocExport)); 
    
    private static string TB(string fallbackEn) => I18N.I.T(fallbackEn, typeof(PandocExport).Namespace, nameof(PandocExport));
    
    public static async Task<bool> ToMicrosoftWord(RustService rustService, IDialogService dialogService, string dialogTitle, IContent markdownContent)
    {
        var response = await rustService.SaveFile(dialogTitle, new("Microsoft Word", ["docx"]));
        if (response.UserCancelled)
        {
            LOGGER.LogInformation("User cancelled the save dialog.");
            return false;
        }

        LOGGER.LogInformation($"The user chose the path '{response.SaveFilePath}' for the Microsoft Word export.");

        var tempMarkdownFilePath = string.Empty;
        try
        {
            var tempMarkdownFile = Guid.NewGuid().ToString();
            tempMarkdownFilePath = Path.Combine(Path.GetTempPath(), tempMarkdownFile);
            
            // Extract text content from chat:
            var markdownText = markdownContent switch
            {
                ContentText text => text.Text,
                ContentImage _ => "Image export to Microsoft Word not yet possible",

                _ => "Unknown content type. Cannot export to Word."
            };

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
                    await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Cancel, TB("Pandoc is required for Microsoft Word export.")));
                    return false;
                }
            }

            // Call Pandoc to create the Word file:
            var pandoc = await PandocProcessBuilder
                .Create()
                .UseStandaloneMode()
                .WithInputFormat("markdown")
                .WithOutputFormat("docx")
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
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Cancel, TB("Error during Microsoft Word export")));
                return false;
            }

            LOGGER.LogInformation("Pandoc conversion successful.");
            await MessageBus.INSTANCE.SendSuccess(new(Icons.Material.Filled.CheckCircle, TB("Microsoft Word export successful")));
            
            return true;
        }
        catch (Exception ex)
        {
            LOGGER.LogError(ex, "Error during Word export.");
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Cancel, TB("Error during Microsoft Word export")));
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
