namespace AIStudio.Assistants.ERI;

/// <summary>
/// What became of a file which the model of the ERI Server assistant created.
/// </summary>
internal enum ERIServerFileOutcome
{
    /// <summary>
    /// The file was written below the base directory.
    /// </summary>
    WRITTEN,

    /// <summary>
    /// The path of the file leads outside the base directory, so the file was not written.
    /// </summary>
    OUTSIDE_BASE_DIRECTORY,

    /// <summary>
    /// The answer holds no complete code block, so there was nothing to write.
    /// </summary>
    NO_COMPLETE_CODE_BLOCK,

    /// <summary>
    /// Writing the file failed, e.g., for lack of permission.
    /// </summary>
    WRITE_FAILED,
}