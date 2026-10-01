namespace AIStudio.Tools.PluginSystem;

public abstract partial class PluginBase
{
    private const string DEFAULT_ICON_SVG =
    """
    <svg height="1.5em" width="1.5em" viewBox="0 0 24 24" fill="#1f1f1f"><path d="M0 0h24v24H0V0z" fill="none"/><path d="M19 13h-2V7h-6V5c0-.28-.22-.5-.5-.5s-.5.22-.5.5v2H4l.01 2.12C5.76 9.8 7 11.51 7 13.5c0 1.99-1.25 3.7-3 4.38V20h2.12c.68-1.75 2.39-3 4.38-3 1.99 0 3.7 1.25 4.38 3H17v-6h2c.28 0 .5-.22.5-.5s-.22-.5-.5-.5z" opacity=".3"/><path d="M19 11V7c0-1.1-.9-2-2-2h-4c0-1.38-1.12-2.5-2.5-2.5S8 3.62 8 5H4c-1.1 0-1.99.9-1.99 2v3.8h.29c1.49 0 2.7 1.21 2.7 2.7s-1.21 2.7-2.7 2.7H2V20c0 1.1.9 2 2 2h3.8v-.3c0-1.49 1.21-2.7 2.7-2.7s2.7 1.21 2.7 2.7v.3H17c1.1 0 2-.9 2-2v-4c1.38 0 2.5-1.12 2.5-2.5S20.38 11 19 11zm0 3h-2v6h-2.12c-.68-1.75-2.39-3-4.38-3-1.99 0-3.7 1.25-4.38 3H4v-2.12c1.75-.68 3-2.39 3-4.38 0-1.99-1.24-3.7-2.99-4.38L4 7h6V5c0-.28.22-.5.5-.5s.5.22.5.5v2h6v6h2c.28 0 .5.22.5.5s-.22.5-.5.5z"/></svg>
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