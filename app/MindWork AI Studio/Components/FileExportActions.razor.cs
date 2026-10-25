using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// The ways from an exported file to the file itself: the file manager, which shows it, and the default
/// program, which opens it.
/// </summary>
/// <remarks>
/// Whoever chose where a file goes wants to get to it afterward, and a message which merely says that
/// the export succeeded leaves them searching. Both ways go through the runtime, like those of the list
/// of sources: under Linux, the file manager is reached through the desktop portal, and the runtime
/// refuses to start programs and launchers.
/// </remarks>
public partial class FileExportActions : MSGComponentBase
{
    /// <summary>
    /// The exported file.
    /// </summary>
    [Parameter]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Whether the file may be opened with the default program. False for an archive, which that
    /// program would only unpack, at least on macOS.
    /// </summary>
    [Parameter]
    public bool OffersOpening { get; set; }

    /// <summary>
    /// Called after the user took one of the ways, whether it worked or not.
    /// </summary>
    [Parameter]
    public EventCallback ActionTaken { get; set; }

    [Inject]
    private RustService RustService { get; init; } = null!;

    [Inject]
    private ILogger<FileExportActions> Logger { get; init; } = null!;

    private async Task ShowInFileManager()
    {
        try
        {
            var response = await this.RustService.TryOpenPathInRuntimeFileManager(this.FilePath);
            if (response.Success)
                return;

            var issue = string.IsNullOrWhiteSpace(response.Issue) ? T("Unknown error") : response.Issue;
            await this.MessageBus.SendError(new(Icons.Material.Filled.FolderOpen, string.Format(T("Could not open the file location: {0}"), issue)));
        }
        catch (Exception e)
        {
            this.Logger.LogWarning(e, "Could not show an exported file in the file manager.");
            await this.MessageBus.SendError(new(Icons.Material.Filled.FolderOpen, T("Could not open the file location.")));
        }
        finally
        {
            await this.ActionTaken.InvokeAsync();
        }
    }

    private async Task OpenWithDefaultProgram()
    {
        try
        {
            var response = await this.RustService.TryOpenDocumentInSystemViewer(this.FilePath, null);
            if (response.Success)
                return;

            var issue = string.IsNullOrWhiteSpace(response.Issue) ? T("Unknown error") : response.Issue;
            await this.MessageBus.SendError(new(Icons.Material.Filled.OpenInNew, string.Format(T("Could not open the file: {0}"), issue)));
        }
        catch (Exception e)
        {
            this.Logger.LogWarning(e, "Could not open an exported file with the default program.");
            await this.MessageBus.SendError(new(Icons.Material.Filled.OpenInNew, T("Could not open the file.")));
        }
        finally
        {
            await this.ActionTaken.InvokeAsync();
        }
    }
}