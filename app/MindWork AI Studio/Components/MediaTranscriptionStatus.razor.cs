using AIStudio.Tools.Services;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class MediaTranscriptionStatus
{
    /// <summary>The surface owner whose operation is rendered.</summary>
    [Parameter]
    public MediaImportOwner Owner { get; set; }

    private MediaImportSnapshot? Snapshot => this.MediaTranscriptionService.GetSnapshot(this.Owner);

    /// <summary>Gets the localized visible status for the active import.</summary>
    private string StatusText => this.Snapshot?.Phase switch
    {
        MediaTranscriptionPhase.QUEUED => $"{this.T("Waiting to prepare media")}: {this.Snapshot.CurrentFileName}",
        MediaTranscriptionPhase.PROBING => $"{this.T("Inspecting media")}: {this.Snapshot.CurrentFileName}",
        MediaTranscriptionPhase.TRANSCODING => $"{this.T("Preparing audio")}: {this.Snapshot.CurrentFileName}",
        MediaTranscriptionPhase.UPLOADING => $"{this.T("Transcribing")}: {this.Snapshot.CurrentFileName}",
        MediaTranscriptionPhase.CANCELING => $"{this.T("Stopping media transcription")}: {this.Snapshot.CurrentFileName}",

        _ => this.Snapshot?.CurrentFileName ?? string.Empty,
    };

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