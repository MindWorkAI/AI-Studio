namespace AIStudio.Settings.DataModel;

/// <summary>
/// AI Studio data source types.
/// </summary>
public enum DataSourceType
{
    /// <summary>
    /// No data source.
    /// </summary>
    NONE = 0,
    
    /// <summary>
    /// One file on the local machine (or a network share).
    /// </summary>
    LOCAL_FILE,
    
    /// <summary>
    /// A directory on the local machine (or a network share).
    /// </summary>
    LOCAL_DIRECTORY,
    
    /// <summary>
    /// External data source accessed via an ERI server, cf. https://github.com/MindWorkAI/ERI.
    /// </summary>
    ERI_V1,
}