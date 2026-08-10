namespace AIStudio.Tools;

/// <summary>
/// Why reading a file failed. The Rust runtime reports these codes as part of the content
/// stream, so the app can tell the user what happened instead of showing an empty document.
/// </summary>
public enum FileExtractionErrorCode
{
    /// <summary>
    /// No failure happened.
    /// </summary>
    NONE,

    /// <summary>
    /// A code this version does not know, e.g. from a newer runtime.
    /// </summary>
    UNKNOWN,

    //
    // Codes reported by the Rust runtime:
    //

    INVALID_REQUEST,
    FILE_NOT_FOUND,
    FILE_NOT_READABLE,

    /// <summary>
    /// Another program holds the file open and denies reading it.
    /// </summary>
    FILE_LOCKED,

    FORMAT_DETECTION_FAILED,
    NOT_A_VALID_PDF,
    NOT_A_VALID_SPREADSHEET,
    PDFIUM_UNAVAILABLE,
    PDF_ENCRYPTED,
    PAGE_EXTRACTION_FAILED,
    NO_TEXT_EXTRACTED,

    /// <summary>
    /// The content does not match the file extension. This is a notice, not a failure: the file
    /// was read according to its content.
    /// </summary>
    EXTENSION_MISMATCH,

    /// <summary>
    /// The file was read as text, but its bytes are not text.
    /// </summary>
    NOT_TEXT_CONTENT,

    /// <summary>
    /// The file is an executable, no matter what its extension claims.
    /// </summary>
    EXECUTABLE_REJECTED,
    UNSUPPORTED,
    INTERNAL,

    //
    // Codes reported by the app itself:
    //

    /// <summary>
    /// Reading the file needs Pandoc, which is not available.
    /// </summary>
    PANDOC_UNAVAILABLE,

    /// <summary>
    /// The runtime answered with an unsuccessful HTTP status.
    /// </summary>
    REQUEST_FAILED,

    /// <summary>
    /// Reading the file took longer than the app is willing to wait.
    /// </summary>
    TIMEOUT,

    /// <summary>
    /// The runtime sent something the app could not deserialize.
    /// </summary>
    INVALID_RESPONSE,

    /// <summary>
    /// The extraction finished without reporting a failure, but produced no content at all.
    /// </summary>
    NO_CONTENT,
}