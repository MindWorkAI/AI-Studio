using System.Diagnostics;
using System.Text;

using AIStudio.Chat;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

namespace AIStudio.Tools;

public static class PandocExport
{
    private static readonly ILogger LOGGER = Program.LOGGER_FACTORY.CreateLogger(nameof(PandocExport));

    private static string TB(string fallbackEn) => I18N.I.T(fallbackEn, typeof(PandocExport).Namespace, nameof(PandocExport));

    /// <summary>
    /// Converts the given Markdown text into a document at the given path.
    /// </summary>
    /// <remarks>
    /// This says nothing to the user: it reports what happened and lets the caller decide. A batch
    /// run over hundreds of documents would otherwise bury the user under notifications. Pandoc
    /// must be available, which PandocAvailabilityService.EnsureAvailabilityAsync takes care of.
    /// </remarks>
    /// <param name="rustService">The Rust service, used to build the Pandoc call.</param>
    /// <param name="markdownText">The Markdown text to convert.</param>
    /// <param name="targetFilePath">Where to write the document.</param>
    /// <param name="format">The format to write. Must be a format which uses Pandoc.</param>
    /// <param name="token">The token to cancel the conversion.</param>
    /// <returns>True, when the document was written.</returns>
    public static async Task<bool> ConvertAsync(RustService rustService, string markdownText, string targetFilePath, FileExportFormat format, CancellationToken token = default)
    {
        if (!format.UsesPandoc())
            throw new ArgumentOutOfRangeException(nameof(format), format, "Pandoc cannot write this format.");

        var tempMarkdownFilePath = string.Empty;
        try
        {
            var tempMarkdownFile = Guid.NewGuid().ToString();
            tempMarkdownFilePath = Path.Combine(Path.GetTempPath(), tempMarkdownFile);

            // Write text content to a temporary file. Pandoc expects UTF-8 without a byte order
            // mark; a mark would end up as a stray character at the start of the document:
            await File.WriteAllTextAsync(tempMarkdownFilePath, markdownText, new UTF8Encoding(false), token);

            // Call Pandoc to create the document:
            var pandoc = await PandocProcessBuilder
                .Create()
                .UseStandaloneMode()
                .WithInputFormat("gfm+emoji+tex_math_dollars")
                .WithOutputFormat(format.ToPandocOutputFormat())
                .WithOutputFile(targetFilePath)
                .WithInputFile(tempMarkdownFilePath)
                .BuildAsync(rustService);

            using var process = Process.Start(pandoc.StartInfo);
            if (process is null)
            {
                LOGGER.LogError("Failed to start Pandoc process.");
                return false;
            }

            // Read output streams asynchronously while the process runs (prevents deadlock):
            var outputTask = process.StandardOutput.ReadToEndAsync(token);
            var errorTask = process.StandardError.ReadToEndAsync(token);

            // Wait for the process to exit AND for streams to be fully read:
            await process.WaitForExitAsync(token);
            await outputTask;
            var error = await errorTask;

            if (process.ExitCode is not 0)
            {
                LOGGER.LogError("Pandoc failed with exit code {ProcessExitCode}: '{ErrorText}'", process.ExitCode, error);
                return false;
            }

            LOGGER.LogInformation("Pandoc conversion to {ExportFormat} successful.", format);
            return true;
        }
        catch (Exception ex)
        {
            LOGGER.LogError(ex, "Error during {ExportFormat} conversion.", format);
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
                    LOGGER.LogWarning("Was not able to delete the temporary file '{TempFilePath}'.", tempMarkdownFilePath);
                }
            }
        }
    }

    /// <summary>
    /// Converts the given content to a document using Pandoc and lets the user save it.
    /// </summary>
    /// <param name="rustService">The Rust service, used for the save dialog and for Pandoc.</param>
    /// <param name="pandocAvailability">Makes sure Pandoc is there and offers its installation.</param>
    /// <param name="dialogTitle">The title of the save dialog. The caller knows what the user is
    /// looking at, a chat message or the result of an assistant, so the caller names it.</param>
    /// <param name="format">The format to write. Must be a format which uses Pandoc.</param>
    /// <param name="markdownContent">The content to export.</param>
    /// <returns>True, when the document was written.</returns>
    public static async Task<bool> ToDocument(RustService rustService, PandocAvailabilityService pandocAvailability, string dialogTitle, FileExportFormat format, IContent markdownContent)
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

        // The service reports a missing Pandoc to the user itself, so we only act on the outcome:
        var pandocState = await pandocAvailability.EnsureAvailabilityAsync(showSuccessMessage: false, showDialog: true);
        if (!pandocState.IsAvailable)
            return false;

        if (!await ConvertAsync(rustService, markdownText, response.SaveFilePath, format))
        {
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Cancel, TB("The export failed.")));
            return false;
        }

        await MessageBus.INSTANCE.SendSuccess(new(Icons.Material.Filled.CheckCircle, TB("The export succeeded.")));
        return true;
    }
}
