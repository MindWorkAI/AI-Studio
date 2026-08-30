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
    private sealed record ExportTarget(string DisplayName, string PandocOutputFormat, FileTypeFilter FileType);

    private static readonly ExportTarget MICROSOFT_WORD = new("Microsoft Word (.docx)", "docx", FileTypes.MS_WORD);
    private static readonly ExportTarget OPEN_DOCUMENT_TEXT = new("OpenDocument Text (.odt)", "odt", FileTypes.ODT);
    private static readonly ExportTarget HTML = new("Hypertext (.html)", "html", FileTypes.HTML);
    private static readonly ExportTarget LATEX = new("LaTeX (.tex)", "latex", FileTypes.TEX);
    
    private static string TB(string fallbackEn) => I18N.I.T(fallbackEn, typeof(PandocExport).Namespace, nameof(PandocExport));
    
    public static async Task<bool> ToDocument(RustService rustService, IDialogService dialogService, FileExportFormat format, IContent markdownContent)
    {
        var exportTarget = format switch
        {
            FileExportFormat.MICROSOFT_WORD => MICROSOFT_WORD,
            FileExportFormat.OPEN_DOCUMENT_TEXT => OPEN_DOCUMENT_TEXT,
            FileExportFormat.HTML => HTML,
            FileExportFormat.LATEX => LATEX,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };

        var response = await rustService.SaveFile(TB("Export chat"), [exportTarget.FileType]);
        if (response.UserCancelled)
        {
            LOGGER.LogInformation("User cancelled the save dialog.");
            return false;
        }

        LOGGER.LogInformation("The user chose the path '{SaveFilePath}' for the {ExportFormat} export.", response.SaveFilePath, exportTarget.DisplayName);

        var tempMarkdownFilePath = string.Empty;
        try
        {
            var tempMarkdownFile = Guid.NewGuid().ToString();
            tempMarkdownFilePath = Path.Combine(Path.GetTempPath(), tempMarkdownFile);
            
            // Extract text content from chat:
            var markdownText = markdownContent switch
            {
                ContentText text => text.Text,
                ContentImage _ => "Image export is not yet possible.",

                _ => "Unknown content type. Cannot export document."
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
                    await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Cancel, TB("Pandoc is required for document export.")));
                    return false;
                }
            }

            // Call Pandoc to create the document:
            var pandoc = await PandocProcessBuilder
                .Create()
                .UseStandaloneMode()
                .WithInputFormat("gfm+emoji+tex_math_dollars")
                .WithOutputFormat(exportTarget.PandocOutputFormat)
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
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Cancel, TB("Error during document export")));
                return false;
            }

            LOGGER.LogInformation("Pandoc conversion successful.");
            await MessageBus.INSTANCE.SendSuccess(new(Icons.Material.Filled.CheckCircle, TB("Document export successful")));
            
            return true;
        }
        catch (Exception ex)
        {
            LOGGER.LogError(ex, "Error during {ExportFormat} export.", exportTarget.DisplayName);
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Cancel, TB("Error during document export")));
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
