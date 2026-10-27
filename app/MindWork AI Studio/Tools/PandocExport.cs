using System.Diagnostics;
using System.Text;

using AIStudio.Tools.Services;

namespace AIStudio.Tools;

public static class PandocExport
{
    private static readonly ILogger LOGGER = Program.LOGGER_FACTORY.CreateLogger(nameof(PandocExport));

    /// <summary>
    /// Converts the given Markdown text into a document at the given path.
    /// </summary>
    /// <remarks>
    /// This says nothing to the user: it reports what happened and lets the caller decide. A batch
    /// run over hundreds of documents would otherwise bury the user under notifications. Pandoc
    /// must be available, which PandocAvailabilityService.EnsureAvailabilityAsync takes care of.
    /// The text is converted as it is: whether it ends with the notice that AI generated it is up
    /// to the caller, which knows what the text is.
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
            var pandocBuilder = PandocProcessBuilder
                .Create()
                .UseStandaloneMode()
                .WithInputFormat("gfm+emoji+tex_math_dollars")
                .WithOutputFormat(format.ToPandocOutputFormat())
                .WithOutputFile(targetFilePath)
                .WithInputFile(tempMarkdownFilePath);

            //
            // The document is named after the file it is written to. Set as metadata, the name
            // reaches the page as a string which Pandoc escapes; only a file named true or false
            // is read as a switch and keeps the temporary name.
            //
            if (format.NeedsPageTitle())
                pandocBuilder.AddArgument("-M").AddArgument($"pagetitle={Path.GetFileNameWithoutExtension(targetFilePath)}");

            var pandoc = await pandocBuilder.BuildAsync(rustService);

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
}
