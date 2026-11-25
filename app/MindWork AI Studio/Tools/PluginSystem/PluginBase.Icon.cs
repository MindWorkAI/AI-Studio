namespace AIStudio.Tools.PluginSystem;

public abstract partial class PluginBase
{
    private const string DEFAULT_ICON_SVG =
    """
    <svg height="1.5em" width="1.5em" viewBox="0 0 24 24" fill="none" stroke="#1f1f1f" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 22v-5"/><path d="M15 8V2"/><path d="M17 8a1 1 0 0 1 1 1v4a4 4 0 0 1-4 4h-4a4 4 0 0 1-4-4V9a1 1 0 0 1 1-1z"/><path d="M9 8V2"/></svg>
    """;
    
    private static readonly string DEFAULT_ICON_DATA_URL = CreateDefaultIconDataUrl();

    #region Initialization-related methods

    /// <summary>
    /// Tries to initialize the icon of the plugin.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When no icon is specified, or when the specified icon is unusable, the default icon will be
    /// used. A plugin never fails to load over its icon.
    /// </para>
    /// <para>
    /// The icon is handed out as a data URL, not as markup: plugins are shown through an image
    /// element so the browser treats their icon as a standalone, script-less document. Rendering
    /// plugin-supplied markup inline would hand every plugin author a way to run code in the app.
    /// </para>
    /// </remarks>
    /// <param name="message">The error message, when the icon could not be read.</param>
    /// <param name="iconDataUrl">The read icon as a data URL.</param>
    /// <returns>True, when the icon could be read successfully.</returns>

    // ReSharper disable once OutParameterValueIsAlwaysDiscarded.Local
    // ReSharper disable once UnusedMethodReturnValue.Local
    private bool TryInitIconDataUrl(out string message, out string iconDataUrl)
    {
        if (!this.State.Environment["ICON_SVG"].TryRead<string>(out var iconSVG))
        {
            iconDataUrl = DEFAULT_ICON_DATA_URL;
            message = "The field ICON_SVG does not exist or is not a valid string.";
            return true;
        }

        if (string.IsNullOrWhiteSpace(iconSVG))
        {
            iconDataUrl = DEFAULT_ICON_DATA_URL;
            message = "The field ICON_SVG is empty. The icon must be a non-empty string.";
            return true;
        }

        if (!SvgIcon.TryCreateDataUrl(iconSVG, out iconDataUrl, out var issue))
        {
            iconDataUrl = DEFAULT_ICON_DATA_URL;
            message = $"The field ICON_SVG is not a usable icon: {issue}";
            return true;
        }

        message = string.Empty;
        return true;
    }

    private static string CreateDefaultIconDataUrl()
    {
        SvgIcon.TryCreateDataUrl(DEFAULT_ICON_SVG, out var dataUrl, out _);
        return dataUrl;
    }

    #endregion
}