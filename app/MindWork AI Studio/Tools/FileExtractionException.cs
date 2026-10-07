namespace AIStudio.Tools;

/// <summary>
/// Thrown when a file could not be read, carrying the stable failure code along with the message.
/// </summary>
/// <remarks>
/// The plain message alone does not say whether another attempt is worth anything. The code does,
/// which is what the indexer needs to tell a file without readable text apart from a network drive
/// which happens to be offline.
/// </remarks>
public sealed class FileExtractionException(FileExtractionErrorCode code, string message, int? pageNumber = null, string? detectedFormat = null) : Exception(message)
{
    /// <summary>
    /// Gets the stable failure code.
    /// </summary>
    public FileExtractionErrorCode Code { get; } = code;

    /// <summary>
    /// Gets the page the failure belongs to, when the failure affects a single page only.
    /// </summary>
    public int? PageNumber { get; } = pageNumber;

    /// <summary>
    /// Gets the format the runtime identified by looking at the content.
    /// </summary>
    public string? DetectedFormat { get; } = detectedFormat;
}