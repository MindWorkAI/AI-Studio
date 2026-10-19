namespace AIStudio.Tools.Services;

/// <summary>
/// What became of sharing or exporting a plugin archive.
/// </summary>
public enum PluginShareOutcome
{
    /// <summary>
    /// The share sheet of the operating system opened for the plugin archive.
    /// </summary>
    SHARED,

    /// <summary>
    /// The plugin archive was written to the location the user chose.
    /// </summary>
    EXPORTED,

    /// <summary>
    /// The user canceled choosing a location for the export.
    /// </summary>
    CANCELLED,

    /// <summary>
    /// The plugin could not be shared or exported.
    /// </summary>
    FAILED,

    /// <summary>
    /// The share sheet of the operating system failed or did not respond. The plugin archive can
    /// still be exported instead.
    /// </summary>
    SHARE_SHEET_UNAVAILABLE,
}