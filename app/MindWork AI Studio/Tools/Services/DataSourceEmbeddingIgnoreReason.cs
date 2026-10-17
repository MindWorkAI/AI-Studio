namespace AIStudio.Tools.Services;

/// <summary>
/// Why a file found while enumerating a local data source was deliberately left out of its index.
/// </summary>
public enum DataSourceEmbeddingIgnoreReason
{
    MEDIA_NOT_SUPPORTED,
    TOO_LARGE,
}