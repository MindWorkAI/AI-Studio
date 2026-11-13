using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// The content of the notification that a file is being exported, see DataFileExportRunningMessage.
/// </summary>
public partial class FileExportRunningMessage : MSGComponentBase
{
    /// <summary>
    /// The name of the file which is being written.
    /// </summary>
    [Parameter]
    public string FileName { get; set; } = string.Empty;
}