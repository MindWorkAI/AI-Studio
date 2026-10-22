namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Why a file path from a configuration plugin cannot be used.
/// </summary>
public enum PluginFilePathIssue
{
    /// <summary>
    /// The path can be used.
    /// </summary>
    NONE,

    /// <summary>
    /// The path is empty.
    /// </summary>
    EMPTY,

    /// <summary>
    /// The path is relative, but the folder of the plugin is unknown.
    /// </summary>
    PLUGIN_PATH_UNKNOWN,

    /// <summary>
    /// The relative path climbs up with '..' segments.
    /// </summary>
    PARENT_SEGMENT,

    /// <summary>
    /// The relative path points outside of the plugin folder.
    /// </summary>
    OUTSIDE_PLUGIN_FOLDER,
}