namespace AIStudio.Tools;

/// <summary>
/// The result of reading a file through the Rust runtime.
/// </summary>
/// <remarks>
/// Content and failure travel together on purpose. When reading a file returns a bare string, a
/// failed extraction is indistinguishable from an empty document, and the empty document reaches
/// the AI as if that were the content of the user's file.
/// </remarks>
/// <param name="Outcome">How the extraction ended.</param>
/// <param name="Content">The extracted content. Empty when the extraction failed.</param>
/// <param name="ErrorCode">Why the extraction failed or lost parts of the file.</param>
/// <param name="ErrorMessage">The technical failure description, meant for logs and diagnostics.</param>
/// <param name="FailedPages">The pages which could not be read, when known.</param>
public readonly record struct FileExtractionResult(FileExtractionOutcome Outcome, string Content, FileExtractionErrorCode ErrorCode, string? ErrorMessage, IReadOnlyList<int> FailedPages)
{
    private static readonly int[] NO_FAILED_PAGES = [];

    public static FileExtractionResult Success(string content) => new(FileExtractionOutcome.SUCCESS, content, FileExtractionErrorCode.NONE, null, NO_FAILED_PAGES);

    public static FileExtractionResult Partial(string content, IReadOnlyList<int> failedPages) => new(FileExtractionOutcome.PARTIAL, content, FileExtractionErrorCode.PAGE_EXTRACTION_FAILED, null, failedPages);

    public static FileExtractionResult Failed(FileExtractionErrorCode errorCode, string? errorMessage) => new(FileExtractionOutcome.FAILED, string.Empty, errorCode, errorMessage, NO_FAILED_PAGES);

    /// <summary>
    /// Gets a value indicating whether the whole file was read.
    /// </summary>
    public bool IsSuccess => this.Outcome is FileExtractionOutcome.SUCCESS;

    /// <summary>
    /// Gets a value indicating whether the content may be handed to the AI, i.e. the extraction
    /// either succeeded or lost only parts of the file.
    /// </summary>
    public bool HasUsableContent => this.Outcome is FileExtractionOutcome.SUCCESS or FileExtractionOutcome.PARTIAL;
}