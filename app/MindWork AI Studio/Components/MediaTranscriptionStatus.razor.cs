using AIStudio.Tools.Media;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class MediaTranscriptionStatus
{
    /// <summary>The surface owner whose operation is rendered.</summary>
    [Parameter]
    public MediaImportOwner Owner { get; set; }

    /// <summary>Optional target filter used by embedded file controls.</summary>
    [Parameter]
    public string TargetId { get; set; } = string.Empty;

    /// <summary>Renders the status without an enclosing paper surface.</summary>
    [Parameter]
    public bool Compact { get; set; }

    private MediaImportSnapshot? Snapshot
    {
        get
        {
            var snapshot = this.MediaTranscriptionService.GetSnapshot(this.Owner);
            return string.IsNullOrWhiteSpace(this.TargetId) || snapshot?.Target.TargetId == this.TargetId
                ? snapshot
                : null;
        }
    }

    /// <summary>Gets the localized visible status for the active import.</summary>
    private string StatusText
    {
        get
        {
            var snapshot = this.Snapshot;
            if (snapshot is null)
                return string.Empty;

            return snapshot.Phase switch
            {
                MediaTranscriptionPhase.QUEUED => $"{this.T("Waiting to prepare media")}: {snapshot.CurrentFileName}",
                MediaTranscriptionPhase.PROBING => $"{this.T("Inspecting media")}: {snapshot.CurrentFileName}",
                MediaTranscriptionPhase.TRANSCODING => $"{this.T("Preparing audio")}: {snapshot.CurrentFileName}",
                MediaTranscriptionPhase.UPLOADING => $"{this.T("Transcribing")}: {snapshot.CurrentFileName}",
                MediaTranscriptionPhase.CANCELING => $"{this.T("Stopping media transcription")}: {snapshot.CurrentFileName}",

                _ => snapshot.CurrentFileName,
            };
        }
    }

    /// <summary>Subscribes to singleton import state changes.</summary>
    protected override async Task OnInitializedAsync()
    {
        this.MediaTranscriptionService.StateChanged += this.OnStateChanged;
        await base.OnInitializedAsync();
    }

    /// <summary>Schedules a render after an import state transition.</summary>
    private void OnStateChanged(MediaImportOwner owner)
    {
        if (owner == this.Owner)
            _ = this.InvokeAsync(this.StateHasChanged);
    }

    /// <summary>Unsubscribes from singleton import state changes.</summary>
    protected override void DisposeResources()
    {
        this.MediaTranscriptionService.StateChanged -= this.OnStateChanged;
        base.DisposeResources();
    }
}