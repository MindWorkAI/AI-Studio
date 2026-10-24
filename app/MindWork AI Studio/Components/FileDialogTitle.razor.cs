using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// The title of a dialog about one file or web page: its name, and at the side, ways to copy its
/// path and to find it in the file manager, see FileActions.
/// </summary>
/// <remarks>
/// The path is what the copy button copies, so that is where it shows, in the tooltip of that
/// button, instead of taking a line of its own. Every dialog about a file names it the same way,
/// which is why this lives here instead of in each of them. A file which is no longer at its place
/// gets a warning instead of the button of the file manager.
/// </remarks>
public partial class FileDialogTitle : MSGComponentBase
{
    /// <summary>
    /// The title, usually the name of the file.
    /// </summary>
    [Parameter]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The path of the file or the address of the web page, or nothing when there is neither to name.
    /// </summary>
    [Parameter]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Whether the path is the address of a web page, which this title then opens in the browser.
    /// </summary>
    private bool OpensWebPages => Uri.TryCreate(this.Path, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}