using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// The content of the notification that a file was exported, see DataFileExportSuccessMessage.
/// </summary>
/// <remarks>
/// Taking one of the ways to the file closes the notification: it has done its job then, and it would
/// otherwise stay, since it waits for the user.
/// </remarks>
public partial class FileExportSuccessMessage : MSGComponentBase
{
    /// <summary>
    /// What was exported.
    /// </summary>
    [Parameter]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The file which was written.
    /// </summary>
    [Parameter]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Whether the file may be opened with the default program.
    /// </summary>
    [Parameter]
    public bool OffersOpening { get; set; }

    /// <summary>
    /// The key of the notification which shows this content.
    /// </summary>
    [Parameter]
    public string SnackbarKey { get; set; } = string.Empty;

    [Inject]
    private ISnackbar Snackbar { get; init; } = null!;

    private void Close() => this.Snackbar.RemoveByKey(this.SnackbarKey);
}