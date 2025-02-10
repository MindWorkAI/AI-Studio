namespace AIStudio.Settings.DataModel;

public enum DataSourceSecurity
{
    /// <summary>
    /// The security of the data source is not specified yet.
    /// </summary>
    NOT_SPECIFIED,
    
    /// <summary>
    /// This data can be used with any LLM provider.
    /// </summary>
    ALLOW_ANY,
    
    /// <summary>
    /// This data can only be used for self-hosted LLM providers.
    /// </summary>
    SELF_HOSTED,
}