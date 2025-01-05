namespace AIStudio.Settings.DataModel;

/// <summary>
/// Extension methods for data source types.
/// </summary>
public static class DataSourceTypeExtension
{
    /// <summary>
    /// Get the display name of the data source type.
    /// </summary>
    /// <param name="type">The data source type.</param>
    /// <returns>The display name of the data source type.</returns>
    public static string GetDisplayName(this DataSourceType type)
    {
        return type switch
        {
            DataSourceType.LOCAL_FILE => "Local File",
            DataSourceType.LOCAL_DIRECTORY => "Local Directory",
            DataSourceType.ERI_V1 => "External ERI Server (v1)",
            
            _ => "None",
        };
    }
}