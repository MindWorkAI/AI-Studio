namespace AIStudio.Tools;

/// <summary>
/// Why reading a file failed. The Rust runtime reports these codes as part of the content
/// stream, so the app can tell the user what happened instead of showing an empty document.
/// </summary>
public enum FileExtractionErrorCode
{
    /// <summary>
    /// A code this version does not know, e.g. from a newer runtime.
    /// </summary>
    UNKNOWN,

    INVALID_REQUEST,
    FILE_NOT_FOUND,
    FILE_NOT_READABLE,
    FORMAT_DETECTION_FAILED,
    NOT_A_VALID_PDF,
    NOT_A_VALID_SPREADSHEET,
    PDFIUM_UNAVAILABLE,
    PDF_ENCRYPTED,
    PAGE_EXTRACTION_FAILED,
    NO_TEXT_EXTRACTED,
    UNSUPPORTED,
    INTERNAL,
}