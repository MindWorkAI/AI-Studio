namespace AIStudio.Tools;

/// <summary>
/// How reading a file ended.
/// </summary>
public enum FileExtractionOutcome
{
    /// <summary>
    /// The whole file was read.
    /// </summary>
    SUCCESS,

    /// <summary>
    /// Parts of the file could not be read, e.g. single pages of a PDF, while the remaining
    /// content is still usable.
    /// </summary>
    PARTIAL,

    /// <summary>
    /// The file could not be read. There is no content the app is allowed to use.
    /// </summary>
    FAILED,
}