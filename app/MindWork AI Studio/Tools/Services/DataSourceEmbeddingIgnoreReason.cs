namespace AIStudio.Tools.Services;

/// <summary>
/// Why a file found while enumerating a local data source was deliberately left out of its index.
/// </summary>
public enum DataSourceEmbeddingIgnoreReason
{
    /// <summary>
    /// An image, which cannot be added to the index yet.
    /// </summary>
    IMAGE_NOT_SUPPORTED,

    /// <summary>
    /// An audio or video file without a transcript, while no usable transcription provider is configured.
    /// </summary>
    TRANSCRIPTION_PROVIDER_MISSING,

    /// <summary>
    /// An audio or video file without a transcript, while the transcription provider falls short of
    /// the confidence level the data source asks for.
    /// </summary>
    TRANSCRIPTION_PROVIDER_NOT_ALLOWED,

    /// <summary>
    /// A file larger than the limit of the data source.
    /// </summary>
    TOO_LARGE,
}