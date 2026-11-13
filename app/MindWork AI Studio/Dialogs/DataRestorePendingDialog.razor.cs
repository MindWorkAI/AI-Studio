using AIStudio.Components;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

/// <summary>
/// Blocks AI Studio while a prepared restore waits for the restart which applies it: whatever the user
/// changed until then would be lost. The user restarts, or discards the restore.
/// </summary>
public partial class DataRestorePendingDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Inject]
    private DataRestoreService DataRestoreService { get; init; } = null!;

    [Inject]
    private RustService RustService { get; init; } = null!;

    private bool isWorking;

    /// <summary>
    /// Whether the runtime could not restart, e.g., in the development environment, where it did not
    /// start the .NET server.
    /// </summary>
    private bool restartFailed;

    private async Task RestartAsync()
    {
        this.isWorking = true;
        try
        {
            // On success, AI Studio ends right away, and this dialog with it:
            this.restartFailed = !await this.RustService.RestartApplication();
        }
        finally
        {
            this.isWorking = false;
        }
    }

    private async Task DiscardAsync()
    {
        this.isWorking = true;
        try
        {
            await this.DataRestoreService.DiscardPendingRestoreAsync();
        }
        finally
        {
            this.isWorking = false;
        }

        if (!this.DataRestoreService.IsRestorePending)
            this.MudDialog.Close(DialogResult.Ok(false));
    }
}