using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools;

/// <summary>
/// Tells failures which lie in the file apart from failures which lie in its surroundings, and
/// puts both into words for the indexing user interface.
/// </summary>
/// <remarks>
/// A file without readable text fails the same way on every run, so the indexer remembers it and
/// waits for the file to change. An offline network drive or an overloaded provider says nothing
/// about the file itself, which is why those keep being retried.
/// </remarks>
internal static class FileExtractionErrorCodeExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(FileExtractionErrorCodeExtensions).Namespace, nameof(FileExtractionErrorCodeExtensions));

    /// <summary>
    /// Gets a value indicating whether reading the file again will fail again, as long as the file
    /// itself does not change.
    /// </summary>
    /// <param name="code">The stable failure code.</param>
    /// <returns>True, when the reason lies in the file itself.</returns>
    internal static bool IsPermanentIndexingFailure(this FileExtractionErrorCode code) => code switch
    {
        //
        // The reason lies in the file. Reading it again without changing it produces the same
        // outcome, so the indexer waits for a new fingerprint:
        //
        FileExtractionErrorCode.NO_TEXT_EXTRACTED => true,
        FileExtractionErrorCode.NO_CONTENT => true,
        FileExtractionErrorCode.NOT_TEXT_CONTENT => true,
        FileExtractionErrorCode.NOT_A_VALID_PDF => true,
        FileExtractionErrorCode.NOT_A_VALID_SPREADSHEET => true,
        FileExtractionErrorCode.PDF_ENCRYPTED => true,
        FileExtractionErrorCode.FORMAT_DETECTION_FAILED => true,
        FileExtractionErrorCode.EXECUTABLE_REJECTED => true,
        FileExtractionErrorCode.UNSUPPORTED => true,

        // Pages holding nothing but images are one of the recurring cases here, and that is a
        // property of the document, not of the environment:
        FileExtractionErrorCode.PAGE_EXTRACTION_FAILED => true,

        //
        // Everything else depends on the surroundings: an unavailable drive, a file someone else
        // has open, a missing engine, or a runtime which did not answer in time. All of them are
        // worth another attempt during the next run:
        //
        _ => false,
    };

    /// <summary>
    /// Names the cause in a few words.
    /// </summary>
    /// <remarks>
    /// Used to group the files of an indexing run by what happened to them: nine hundred entries
    /// which all say the same sentence are one cause, not nine hundred.
    /// </remarks>
    /// <param name="code">The stable failure code.</param>
    /// <returns>The localized name of the cause, or an empty text when the code has none.</returns>
    internal static string GetIndexingCauseName(this FileExtractionErrorCode code) => code switch
    {
        FileExtractionErrorCode.NO_TEXT_EXTRACTED => TB("No readable text"),
        FileExtractionErrorCode.NO_CONTENT => TB("No content"),
        FileExtractionErrorCode.NOT_TEXT_CONTENT => TB("Not a text file"),
        FileExtractionErrorCode.NOT_A_VALID_PDF => TB("Not a readable PDF"),
        FileExtractionErrorCode.NOT_A_VALID_SPREADSHEET => TB("Not a readable spreadsheet"),
        FileExtractionErrorCode.PDF_ENCRYPTED => TB("Protected PDF"),
        FileExtractionErrorCode.FORMAT_DETECTION_FAILED => TB("Unknown file type"),
        FileExtractionErrorCode.EXECUTABLE_REJECTED => TB("Executable program"),
        FileExtractionErrorCode.UNSUPPORTED => TB("Unsupported file type"),
        FileExtractionErrorCode.PAGE_EXTRACTION_FAILED => TB("Pages without readable text"),

        FileExtractionErrorCode.FILE_NOT_FOUND => TB("File does not exist anymore"),
        FileExtractionErrorCode.FILE_NOT_READABLE => TB("File could not be read"),
        FileExtractionErrorCode.FILE_LOCKED => TB("File is open elsewhere"),
        FileExtractionErrorCode.TIMEOUT => TB("Reading took too long"),
        FileExtractionErrorCode.PDFIUM_UNAVAILABLE => TB("PDF system unavailable"),
        FileExtractionErrorCode.PANDOC_UNAVAILABLE => TB("Pandoc unavailable"),

        // Nothing about these lies in the file: AI Studio asked its runtime for the content and
        // got back something it cannot work with. One name for all of them, because that is the
        // one thing the user can tell from them:
        FileExtractionErrorCode.INVALID_RESPONSE => TB("Internal error"),
        FileExtractionErrorCode.INVALID_REQUEST => TB("Internal error"),
        FileExtractionErrorCode.REQUEST_FAILED => TB("Internal error"),
        FileExtractionErrorCode.INTERNAL => TB("Internal error"),

        // Codes which say nothing beyond the message of the single file. The caller names those
        // files itself and shows their messages instead:
        _ => string.Empty,
    };

    /// <summary>
    /// Gets the localized message which explains why a file was not indexed.
    /// </summary>
    /// <remarks>
    /// These texts are the counterpart of the ones used for chat attachments: there, a file which
    /// cannot be read is simply not sent, while here it stays out of the index and the user needs
    /// to know whether AI Studio will come back to it on its own.
    /// </remarks>
    /// <param name="code">The stable failure code.</param>
    /// <param name="fileName">The name of the file, as shown to the user.</param>
    /// <returns>The localized message.</returns>
    internal static string ToIndexingUserMessage(this FileExtractionErrorCode code, string fileName) => string.Format(ToIndexingMessageFormat(code), fileName);

    private static string ToIndexingMessageFormat(FileExtractionErrorCode code) => code switch
    {
        //
        // Permanent failures. Each of them names what is wrong with the file and says that AI
        // Studio comes back to it once the file changes:
        //
        FileExtractionErrorCode.NO_TEXT_EXTRACTED => TB("No text could be read from the file '{0}', so it was not indexed. It might contain images only, such as a scanned PDF without a text layer. AI Studio reads it again as soon as the file changes."),
        FileExtractionErrorCode.NO_CONTENT => TB("The file '{0}' did not provide any content, so it was not indexed. AI Studio reads it again as soon as the file changes."),
        FileExtractionErrorCode.NOT_TEXT_CONTENT => TB("The file '{0}' is not a text file, so it was not indexed. Its content could not be read as text, which means it might have a wrong file extension. AI Studio reads it again as soon as the file changes."),
        FileExtractionErrorCode.NOT_A_VALID_PDF => TB("The file '{0}' is not a readable PDF, so it was not indexed. It might be damaged or transferred incompletely. AI Studio reads it again as soon as the file changes."),
        FileExtractionErrorCode.NOT_A_VALID_SPREADSHEET => TB("The file '{0}' is not a readable spreadsheet, so it was not indexed. It might be damaged or transferred incompletely. AI Studio reads it again as soon as the file changes."),
        FileExtractionErrorCode.PDF_ENCRYPTED => TB("The file '{0}' is protected and could not be opened, so it was not indexed. AI Studio reads it again as soon as the file changes."),
        FileExtractionErrorCode.FORMAT_DETECTION_FAILED => TB("The file type of '{0}' could not be determined, so the file was not indexed. AI Studio reads it again as soon as the file changes."),
        FileExtractionErrorCode.EXECUTABLE_REJECTED => TB("The file '{0}' is an executable program and was not indexed, regardless of its file extension."),
        FileExtractionErrorCode.UNSUPPORTED => TB("The file type of '{0}' is not supported, so the file was not indexed. AI Studio reads it again as soon as the file changes."),
        FileExtractionErrorCode.PAGE_EXTRACTION_FAILED => TB("Pages of the file '{0}' could not be read, so it was not indexed. They might contain images only. AI Studio reads it again as soon as the file changes."),

        //
        // Temporary failures. They name what the user can act on, and every one of them is tried
        // again during the next run:
        //
        FileExtractionErrorCode.FILE_NOT_FOUND => TB("The file '{0}' does not exist anymore and was not indexed."),
        FileExtractionErrorCode.FILE_NOT_READABLE => TB("The file '{0}' could not be read and was not indexed. When the file is stored on a network drive, the drive might be unavailable, or another program might be blocking the file. AI Studio tries again during the next run."),
        FileExtractionErrorCode.FILE_LOCKED => TB("The file '{0}' is currently open in another program, which is why it was not indexed. When the file is stored on a shared network drive, a colleague might have it open. AI Studio tries again during the next run."),
        FileExtractionErrorCode.TIMEOUT => TB("Reading the file '{0}' took too long and was stopped, so the file was not indexed. When the file is stored on a network drive, the connection might be slow or interrupted. AI Studio tries again during the next run."),
        FileExtractionErrorCode.PDFIUM_UNAVAILABLE => TB("AI Studio was not able to start its PDF engine, so the file '{0}' was not indexed. AI Studio tries again during the next run."),
        FileExtractionErrorCode.PANDOC_UNAVAILABLE => TB("Reading the file '{0}' needs Pandoc, which is not available, so the file was not indexed. AI Studio tries again during the next run."),

        _ => TB("The file '{0}' could not be read and was not indexed. AI Studio tries again during the next run."),
    };
}