using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// The title of a dialog about one file: its name, and at the side, ways to copy its path or to
/// find it in the file manager.
/// </summary>
/// <remarks>
/// The path is what the copy button copies, so that is where it shows, in the tooltip of that
/// button, instead of taking a line of its own. Every dialog about a file names it the same way,
/// which is why this lives here instead of in each of them.
/// </remarks>
public partial class FileDialogTitle : MSGComponentBase
{
    /// <summary>
    /// The title, usually the name of the file.
    /// </summary>
    [Parameter]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The path of the file, or nothing when there is no file to name.
    /// </summary>
    [Parameter]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Whether the file can be shown in the file manager, i.e., whether it is a file on this computer.
    /// </summary>
    [Parameter]
    public bool OffersFileManager { get; set; }

    [Inject]
    private RustService RustService { get; init; } = null!;

    private async Task CopyPath() => await this.RustService.CopyText2Clipboard(this.Path);

    private async Task ShowInFileManager() => await new SourceDocumentLocation(this.Path, null).ShowInFileManagerAsync(this.RustService);
}