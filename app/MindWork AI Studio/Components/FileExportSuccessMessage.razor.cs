using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// The content of the notification that a file was exported, see DataFileExportSuccessMessage.
/// </summary>
/// <remarks>
/// Taking one of the ways to the file leaves the notification open: whoever opened the file often
/// wants to see it in its folder afterward, or the other way round.
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
}