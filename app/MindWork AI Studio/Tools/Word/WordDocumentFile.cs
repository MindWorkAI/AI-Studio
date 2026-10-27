using AIStudio.Tools.OpenXml;

namespace AIStudio.Tools.Word;

/// <summary>
/// Creates a Word document from a template and a Markdown text.
/// </summary>
/// <remarks>
/// The document comes about in a TemplateCopy, and only the complete document is streamed into the
/// file the user chose; TemplateCopy says why.
/// </remarks>
internal static class WordDocumentFile
{
    /// <summary>
    /// The folder below the temporary folder of the system, where the documents come about.
    /// </summary>
    private const string TEMPORARY_DIRECTORY = "mindwork-ai-studio-documents";

    /// <summary>
    /// Writes the Markdown into a copy of the template, and the result into the target.
    /// </summary>
    /// <remarks>
    /// Throws when the template cannot be read or is no Word document, and when the target cannot
    /// be written, e.g., because Word shows it; the caller decides what the user hears about it.
    /// </remarks>
    /// <param name="templatePath">The path of the template, a .docx or a .dotx.</param>
    /// <param name="targetPath">The path of the file the user chose. It may exist; the save dialog asked about overwriting it.</param>
    /// <param name="markdown">The Markdown text to write.</param>
    /// <param name="logger">Where to report a temporary file which cannot be removed.</param>
    /// <param name="token">The cancellation token.</param>
    public static async Task CreateAsync(string templatePath, string targetPath, string markdown, ILogger logger, CancellationToken token)
    {
        using var copy = await TemplateCopy.CreateAsync(templatePath, TEMPORARY_DIRECTORY, ".docx", logger, token);
        await using (var documentFile = copy.OpenForEditing())
            WordDocumentWriter.Write(documentFile, markdown);

        await copy.WriteIntoAsync(targetPath, token);
    }
}