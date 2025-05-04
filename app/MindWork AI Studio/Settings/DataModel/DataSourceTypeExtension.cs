using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// Extension methods for data source types.
/// </summary>
public static class DataSourceTypeExtension
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(DataSourceTypeExtension).Namespace, nameof(DataSourceTypeExtension));
    
    /// <summary>
    /// Get the display name of the data source type.
    /// </summary>
    /// <param name="type">The data source type.</param>
    /// <returns>The display name of the data source type.</returns>
    public static string GetDisplayName(this DataSourceType type) => type switch
    {
        DataSourceType.LOCAL_FILE => TB("Local File"),
        DataSourceType.LOCAL_DIRECTORY => TB("Local Directory"),
        DataSourceType.ERI_V1 => TB("External ERI Server (v1)"),

        _ => TB("None"),
    };
}