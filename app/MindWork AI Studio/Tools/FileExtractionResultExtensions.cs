using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools;

/// <summary>
/// Translates the stable failure codes of a file extraction into user-facing text.
/// </summary>
/// <remarks>
/// The message which travels with a result is technical: it comes from the runtime, names the
/// library which failed, and belongs into the log. The texts here are the counterpart for the
/// user, and they name what the user can act on, such as an unavailable network drive.
/// </remarks>
internal static class FileExtractionResultExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(FileExtractionResultExtensions).Namespace, nameof(FileExtractionResultExtensions));

    /// <summary>
    /// Gets the localized message which explains why a file could not be read.
    /// </summary>
    /// <param name="result">The extraction result.</param>
    /// <param name="fileName">The name of the file, as shown to the user.</param>
    /// <returns>The localized message.</returns>
    internal static string ToUserMessage(this FileExtractionResult result, string fileName) => string.Format(ToUserMessageFormat(result.ErrorCode), fileName);

    /// <summary>
    /// Gets the localized message for a file which was read, but lost some of its pages.
    /// </summary>
    /// <param name="result">The extraction result.</param>
    /// <param name="fileName">The name of the file, as shown to the user.</param>
    /// <returns>The localized message.</returns>
    internal static string ToPartialUserMessage(this FileExtractionResult result, string fileName)
    {
        if (result.FailedPages.Count == 0)
            return string.Format(TB("Parts of the file '{0}' could not be read. The remaining content was sent."), fileName);

        return string.Format(TB("The pages {1} of the file '{0}' could not be read. The remaining content was sent."), fileName, string.Join(", ", result.FailedPages));
    }

    private static string ToUserMessageFormat(FileExtractionErrorCode code) => code switch
    {
        FileExtractionErrorCode.FILE_NOT_FOUND => TB("The file '{0}' does not exist anymore and was not sent."),
        FileExtractionErrorCode.FILE_NOT_READABLE => TB("The file '{0}' could not be read and was not sent. When the file is stored on a network drive, the drive might be unavailable, or another program might be blocking the file."),
        FileExtractionErrorCode.TIMEOUT => TB("Reading the file '{0}' took too long and was stopped, so the file was not sent. When the file is stored on a network drive, the connection might be slow or interrupted."),
        FileExtractionErrorCode.NOT_A_VALID_PDF => TB("The file '{0}' is not a readable PDF and was not sent. It might be damaged or transferred incompletely."),
        FileExtractionErrorCode.NOT_A_VALID_SPREADSHEET => TB("The file '{0}' is not a readable spreadsheet and was not sent. It might be damaged or transferred incompletely."),
        FileExtractionErrorCode.PDF_ENCRYPTED => TB("The file '{0}' is protected and could not be opened, so it was not sent."),
        FileExtractionErrorCode.PDFIUM_UNAVAILABLE => TB("AI Studio was not able to start its PDF engine, so the file '{0}' was not sent."),
        FileExtractionErrorCode.NO_TEXT_EXTRACTED => TB("No text could be read from the file '{0}', so it was not sent. The file might consist of scanned images without a text layer."),
        FileExtractionErrorCode.NO_CONTENT => TB("The file '{0}' did not provide any content and was not sent."),
        FileExtractionErrorCode.FORMAT_DETECTION_FAILED => TB("The file type of '{0}' could not be determined, so the file was not sent."),
        FileExtractionErrorCode.UNSUPPORTED => TB("The file type of '{0}' is not supported, so the file was not sent."),

        _ => TB("The file '{0}' could not be read and was not sent."),
    };
}