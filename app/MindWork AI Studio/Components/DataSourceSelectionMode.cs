namespace AIStudio.Components;

public enum DataSourceSelectionMode
{
    /// <summary>
    /// The user is selecting data sources for, e.g., the chat.
    /// </summary>
    /// <remarks>
    /// In this case, we have to filter the data sources based on the
    /// selected provider and check security requirements.
    /// </remarks>
    SELECTION_MODE,
    
    /// <summary>
    /// The user is configuring the default data sources, e.g., for the chat.
    /// </summary>
    /// <remarks>
    /// In this case, all data sources are available for selection.
    /// They get filtered later based on the selected provider and
    /// security requirements.
    /// </remarks>
    CONFIGURATION_MODE,
}