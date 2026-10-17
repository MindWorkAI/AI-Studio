namespace AIStudio.Tools.DataBackup;

/// <summary>
/// What the user still has to do after a restore, since a backup holds no secrets and another computer
/// may lack the files the settings name.
/// </summary>
public enum DataRestoreOpenPointKind
{
    /// <summary>
    /// A provider of the user has no API key on this computer.
    /// </summary>
    PROVIDER_WITHOUT_API_KEY,

    /// <summary>
    /// An embedding provider of the user has no API key on this computer.
    /// </summary>
    EMBEDDING_PROVIDER_WITHOUT_API_KEY,

    /// <summary>
    /// A transcription provider of the user has no API key on this computer.
    /// </summary>
    TRANSCRIPTION_PROVIDER_WITHOUT_API_KEY,

    /// <summary>
    /// An ERI data source of the user has no password or token on this computer.
    /// </summary>
    DATA_SOURCE_WITHOUT_SECRET,

    /// <summary>
    /// A mailbox of the user has no password on this computer.
    /// </summary>
    MAILBOX_WITHOUT_PASSWORD,

    /// <summary>
    /// A tool which the user set up has no value for a secret setting on this computer, e.g., for
    /// the API key of a search service.
    /// </summary>
    TOOL_WITHOUT_SECRET,

    /// <summary>
    /// A local data source names a file or folder which does not exist on this computer.
    /// </summary>
    DATA_SOURCE_NOT_FOUND,

    /// <summary>
    /// The batch processing names a file or folder which does not exist on this computer.
    /// </summary>
    BATCH_PROCESSING_PATH_NOT_FOUND,

    /// <summary>
    /// The file with custom root certificates does not exist on this computer.
    /// </summary>
    CERTIFICATE_BUNDLE_NOT_FOUND,

    /// <summary>
    /// The ERI server assistant writes a server into a folder which does not exist on this computer.
    /// </summary>
    ERI_SERVER_DIRECTORY_NOT_FOUND,
}