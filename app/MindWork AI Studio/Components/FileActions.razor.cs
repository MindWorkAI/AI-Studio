using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// What the user can do with a file or a web page which a source, a passage, or an attachment
/// names: copy its path, find it in the file manager, open it, at a page where there is one.
/// </summary>
/// <remarks>
/// Every place which names a file offers the same buttons the same way, which is why they live
/// here. Each place decides which of them it shows, e.g., a title copies and finds, while the bar
/// above a text opens.<br/><br/>
///
/// A file which is no longer at its place gets a warning instead of the buttons which would only
/// fail; copying its path stays, since it tells where the file was. Whether it is there is asked
/// off the UI thread and once per path: on an unreachable network share, the answer takes until
/// the timeout of the share, and a list of sources renders anew with every streamed chunk.<br/><br/>
///
/// A web page is copied and opened in the browser. Anything else, such as a mail, which only
/// AI Studio can find by its address, or a relative path, gets no buttons at all.
/// </remarks>
public partial class FileActions : MSGComponentBase
{
    /// <summary>
    /// What a path names.
    /// </summary>
    internal enum FileActionsKind
    {
        NONE,
        FILE,
        WEB_PAGE,
    }

    /// <summary>
    /// The path of the file, or the address of the web page.
    /// </summary>
    [Parameter]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// The page to open a file at, or null to open it at its beginning.
    /// </summary>
    [Parameter]
    public int? PageNumber { get; set; }

    [Parameter]
    public bool ShowCopy { get; set; } = true;

    [Parameter]
    public bool ShowFileManager { get; set; } = true;

    [Parameter]
    public bool ShowOpen { get; set; } = true;

    /// <summary>
    /// The tooltip of the button which opens a file, or null for the one which fits the page.
    /// </summary>
    [Parameter]
    public string? OpenTooltip { get; set; }

    [Parameter]
    public Placement TooltipPlacement { get; set; } = Placement.Top;

    /// <summary>
    /// Tells the caller whether the file is at its place, once that is known, e.g., so that a title
    /// which opens it stops being a link.
    /// </summary>
    [Parameter]
    public EventCallback<bool> OnExistenceChecked { get; set; }

    [Inject]
    private RustService RustService { get; init; } = null!;

    private FileActionsKind kind;

    /// <summary>
    /// The path whose existence was asked last, so that a render with the same path does not ask again.
    /// </summary>
    private string? checkedPath;

    /// <summary>
    /// Whether the file is known to be gone. Until the answer arrives, the buttons show: most files are there.
    /// </summary>
    private bool isMissing;

    private string OpenTooltipText => this.OpenTooltip ?? (this.PageNumber is { } page
        ? string.Format(T("Opens the file at page {0}"), page)
        : T("Opens the file in the program your system uses for it"));

    #region Overrides of ComponentBase

    protected override async Task OnParametersSetAsync()
    {
        this.kind = DetermineKind(this.Path);
        if (this.kind is FileActionsKind.FILE && !string.Equals(this.checkedPath, this.Path, StringComparison.Ordinal))
        {
            var path = this.Path;
            this.checkedPath = path;
            this.isMissing = false;

            var exists = await Task.Run(() => File.Exists(path));

            // Another path took over meanwhile, which asks for itself:
            if (!string.Equals(this.checkedPath, path, StringComparison.Ordinal))
                return;

            this.isMissing = !exists;
            await this.OnExistenceChecked.InvokeAsync(exists);
        }

        await base.OnParametersSetAsync();
    }

    #endregion

    /// <summary>
    /// Tells a web page from a file on this computer by its path.
    /// </summary>
    /// <remarks>
    /// Only an absolute path names a file: a relative one would name another file depending on
    /// where it is opened from.
    /// </remarks>
    internal static FileActionsKind DetermineKind(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return FileActionsKind.NONE;

        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            return FileActionsKind.WEB_PAGE;

        return System.IO.Path.IsPathRooted(path) && (uri is null || uri.IsFile)
            ? FileActionsKind.FILE
            : FileActionsKind.NONE;
    }

    private async Task CopyPath() => await this.RustService.CopyText2Clipboard(this.Path);

    private async Task ShowInFileManager() => await new SourceDocumentLocation(this.Path, null).ShowInFileManagerAsync(this.RustService);

    private async Task Open() => await new SourceDocumentLocation(this.Path, this.PageNumber).OpenAsync(this.RustService);
}