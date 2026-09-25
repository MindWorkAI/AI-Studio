using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

/// <summary>
/// Asks whether the files AI Studio owns for the selected chats are written into the archive.
/// </summary>
/// <remarks>
/// The dialog is only shown when such files exist at all. Its result distinguishes three
/// answers: include them, export the chats alone, or do not export at all.
/// </remarks>
public partial class ChatArchiveFilesDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    private void Cancel() => this.MudDialog.Cancel();

    private void ExcludeFiles() => this.MudDialog.Close(DialogResult.Ok(false));

    private void IncludeFiles() => this.MudDialog.Close(DialogResult.Ok(true));
}
