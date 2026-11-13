using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// Extension methods for data source types.
/// </summary>
public static class DataSourceTypeExtension
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(DataSourceTypeExtension).Namespace, nameof(DataSourceTypeExtension));
    
    /// <param name="type">The data source type.</param>
    extension(DataSourceType type)
    {
        /// <summary>
        /// Get the display name of the data source type.
        /// </summary>
        /// <value>The display name of the data source type.</value>
        public string DisplayName => type switch
        {
            DataSourceType.LOCAL_FILE => TB("Local File"),
            DataSourceType.LOCAL_DIRECTORY => TB("Local Directory"),
            DataSourceType.ERI_V1 => TB("External ERI Server (v1)"),
            DataSourceType.MAILBOX => TB("Mailbox"),

            _ => TB("None"),
        };
    }
}