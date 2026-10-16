using AIStudio.Assistants.BatchProcessing;
using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

/// <summary>
/// Asks the user whether a previous batch run should be continued or started from scratch.
/// </summary>
public partial class BatchProcessingResumeDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// The number of documents which were processed successfully during the previous run.
    /// </summary>
    [Parameter]
    public int NumCompletedFiles { get; set; }

    /// <summary>
    /// The number of documents which still need to be processed.
    /// </summary>
    [Parameter]
    public int NumRemainingFiles { get; set; }

    /// <summary>
    /// The number of documents which the log lists as successfully processed,
    /// but whose results no longer exist. They count as remaining and are
    /// processed again when the run is continued.
    /// </summary>
    [Parameter]
    public int NumMissingResults { get; set; }

    private void Cancel() => this.MudDialog.Cancel();

    private void Continue() => this.MudDialog.Close(DialogResult.Ok(BatchProcessingResumeDecision.CONTINUE));

    private void Restart() => this.MudDialog.Close(DialogResult.Ok(BatchProcessingResumeDecision.RESTART));
}