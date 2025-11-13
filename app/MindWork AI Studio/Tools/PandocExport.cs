using System.Diagnostics;
using AIStudio.Chat;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

namespace AIStudio.Tools;

public static class PandocExport
{
    private static readonly ILogger LOGGER = Program.LOGGER_FACTORY.CreateLogger(nameof(PandocExport)); 
    
    private static string TB(string fallbackEn) => I18N.I.T(fallbackEn, typeof(PandocExport).Namespace, nameof(PandocExport));
    
    public static async Task<bool> ToMicrosoftWord(RustService rustService, string dialogTitle, IContent markdownContent)
    {
        var response = await rustService.SaveFile(dialogTitle, new("Microsoft Word", ["docx"]));
        if (response.UserCancelled)
        {
            LOGGER.LogInformation("User cancelled the save dialog.");
            return false;
        }

        LOGGER.LogInformation($"The user chose the path '{response.SaveFilePath}' for the Microsoft Word export.");

        var tempMarkdownFile = Guid.NewGuid().ToString();
        var tempMarkdownFilePath = Path.Combine(Path.GetTempPath(), tempMarkdownFile);

        try
        {
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
            var pandocState = await Pandoc.CheckAvailabilityAsync(rustService);
            if (!pandocState.IsAvailable)
                return false;

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

            await process.WaitForExitAsync();
            if (process.ExitCode is not 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                LOGGER.LogError($"Pandoc failed with exit code {process.ExitCode}: {error}");
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
