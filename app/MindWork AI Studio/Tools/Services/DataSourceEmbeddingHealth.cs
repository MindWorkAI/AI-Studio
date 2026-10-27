namespace AIStudio.Tools.Services;

/// <summary>
/// Whether a settled data source can be used as-is or needs the user's attention.
/// </summary>
public enum DataSourceEmbeddingHealth
{
    HEALTHY,
    WARNING,
    ERROR,
}
