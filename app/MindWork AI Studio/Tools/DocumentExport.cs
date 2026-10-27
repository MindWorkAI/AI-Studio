using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;
using AIStudio.Tools.Word;

namespace AIStudio.Tools;

/// <summary>
/// Exports content as a document the user saves, e.g., a Word file.
/// </summary>
/// <remarks>
/// A document comes about in one of two ways. Exported into a format template, AI Studio writes it
/// into a copy of the template itself, see Tools/Word. Without a template, Pandoc converts the
/// content. Everything around the conversion is the same for both: the text, the notice that AI
/// generated it, the save dialog, and what the user hears about the export.
/// </remarks>
public static class DocumentExport
{
    private static readonly ILogger LOGGER = Program.LOGGER_FACTORY.CreateLogger(nameof(DocumentExport));

    private static string TB(string fallbackEn) => I18N.I.T(fallbackEn, typeof(DocumentExport).Namespace, nameof(DocumentExport));

    /// <summary>
    /// Converts the given content to a document and lets the user save it.
    /// </summary>
    /// <param name="rustService">The Rust service, used for the save dialog and for Pandoc.</param>
    /// <param name="pandocAvailability">Makes sure Pandoc is there and offers its installation.</param>
    /// <param name="disclosureService">Ends the text with the notice that AI generated it, when that is
    /// switched on. Null for content which never gets the notice, such as the answer of the prompt
    /// optimizer, which is meant for another model.</param>
    /// <param name="dialogTitle">The title of the save dialog. The caller knows what the user is
    /// looking at, a chat message or the result of an assistant, so the caller names it.</param>
    /// <param name="format">The format to write. Must be a format which uses Pandoc.</param>
    /// <param name="markdownContent">The content to export.</param>
    /// <param name="fileName">What the document is about, used to suggest a name in the save dialog.
    /// Null falls back to a generic name.</param>
    /// <param name="formatTemplate">The template to export into; null to let Pandoc write the document.
    /// It has to serve the format, cf. FormatTemplate.Serves.</param>
    /// <returns>True, when the document was written.</returns>
    public static async Task<bool> ToDocument(RustService rustService, PandocAvailabilityService pandocAvailability, AIGeneratedContentDisclosureService? disclosureService, string dialogTitle, FileExportFormat format, IContent markdownContent, string? fileName = null, FormatTemplate? formatTemplate = null)
    {
        if (!format.UsesPandoc() || format.ToFileTypeFilter() is not { } fileTypeFilter)
            throw new ArgumentOutOfRangeException(nameof(format), format, "Pandoc cannot write this format.");

        if (formatTemplate is not null && !formatTemplate.Serves(format))
            throw new ArgumentOutOfRangeException(nameof(formatTemplate), formatTemplate.FilePath, $"The format template cannot serve the format {format}.");

        //
        // We read the text before we ask for a path: when there is nothing to convert, the user
        // should learn that right away instead of picking a file first and getting an error afterwards.
        //
        if (!markdownContent.TryGetExportMarkdown(out var markdownText, format.FollowsPageAnchors()))
        {
            LOGGER.LogWarning("Cannot export the content as {ExportFormat}, because it carries no text.", format);
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Cancel, TB("Only text messages can be exported.")));
            return false;
        }

        markdownText = disclosureService?.AddDisclosureToMarkdown(markdownText) ?? markdownText;

        //
        // For the same reason, we look for the template first. On a background thread, because a
        // template on a network share which is not connected can take a while to answer:
        //
        if (formatTemplate is not null && !await Task.Run(() => File.Exists(formatTemplate.FilePath)))
        {
            LOGGER.LogWarning("The file '{FilePath}' of the format template '{FormatTemplateName}' cannot be found.", formatTemplate.FilePath, formatTemplate.Name);
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Cancel, string.Format(TB("The file of the format template '{0}' cannot be found. Please check the template in the settings."), formatTemplate.Name)));
            return false;
        }

        var response = await rustService.SaveFile(dialogTitle, [fileTypeFilter], format.ToSuggestedFileName(fileName));
        if (response.UserCancelled)
        {
            LOGGER.LogInformation("User cancelled the save dialog.");
            return false;
        }

        LOGGER.LogInformation("The user chose the path '{SaveFilePath}' for the {ExportFormat} export.", response.SaveFilePath, format);

        // Only Pandoc needs to be installed. The service reports a missing Pandoc to the user itself, so we only act on the outcome:
        if (formatTemplate is null && !(await pandocAvailability.EnsureAvailabilityAsync(showSuccessMessage: false, showDialog: true)).IsAvailable)
            return false;

        //
        // The export runs from here on. Before, the user may still be looking at the dialog which
        // offers to install Pandoc, and a notification that we save would be wrong there:
        //
        await using var feedback = new FileExportFeedback();
        feedback.ShowRunning(response.SaveFilePath);
        var isWritten = formatTemplate is null
            ? await PandocExport.ConvertAsync(rustService, markdownText, response.SaveFilePath, format)
            : await WriteIntoTemplateAsync(formatTemplate, markdownText, response.SaveFilePath);

        if (!isWritten)
        {
            var message = formatTemplate is null
                ? TB("The export failed.")
                : string.Format(TB("The export into the format template '{0}' failed. Please check that the template can be read and that the target file is not open in another program."), formatTemplate.Name);

            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Cancel, message));
            return false;
        }

        await feedback.SucceededAsync(Icons.Material.Filled.CheckCircle, TB("The export succeeded."), response.SaveFilePath, true);
        return true;
    }

    /// <summary>
    /// Writes the Markdown into a copy of the template, and the result into the target.
    /// </summary>
    /// <remarks>
    /// Only Word templates serve an export so far, cf. FileExportFormatExtensions.ToFormatTemplateFilter,
    /// so the Word writer takes every template. Like PandocExport.ConvertAsync, this says nothing to
    /// the user and reports only whether the document was written.
    /// </remarks>
    private static async Task<bool> WriteIntoTemplateAsync(FormatTemplate formatTemplate, string markdownText, string targetPath)
    {
        try
        {
            await WordDocumentFile.CreateAsync(formatTemplate.FilePath, targetPath, markdownText, LOGGER, CancellationToken.None);
            LOGGER.LogInformation("Exporting into the format template '{FormatTemplateName}' succeeded.", formatTemplate.Name);
            return true;
        }
        catch (Exception e)
        {
            LOGGER.LogError(e, "Exporting into the format template '{FormatTemplateName}' at '{FilePath}' failed.", formatTemplate.Name, formatTemplate.FilePath);
            return false;
        }
    }
}