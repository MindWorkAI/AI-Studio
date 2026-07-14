using AIStudio.Tools.Services;

namespace AIStudio.Components;

public partial class MediaTranscriptionStatus
{
    /// <summary>Gets the localized visible status for the active import.</summary>
    private string StatusText => this.MediaTranscriptionService.Phase switch
    {
        MediaTranscriptionPhase.PROBING => $"{this.T("Inspecting media")}: {this.MediaTranscriptionService.CurrentFileName}",
        MediaTranscriptionPhase.TRANSCODING => $"{this.T("Preparing audio")}: {this.MediaTranscriptionService.CurrentFileName}",
        MediaTranscriptionPhase.UPLOADING => $"{this.T("Transcribing")}: {this.MediaTranscriptionService.CurrentFileName}",

        _ => this.MediaTranscriptionService.CurrentFileName,
    };

    /// <summary>Subscribes to singleton import state changes.</summary>
    protected override async Task OnInitializedAsync()
    {
        this.MediaTranscriptionService.StateChanged += this.OnStateChanged;
        await base.OnInitializedAsync();
    }

    /// <summary>Schedules a render after an import state transition.</summary>
    private void OnStateChanged() => _ = this.InvokeAsync(this.StateHasChanged);

    /// <summary>Unsubscribes from singleton import state changes.</summary>
    protected override void DisposeResources()
    {
        this.MediaTranscriptionService.StateChanged -= this.OnStateChanged;
        base.DisposeResources();
    }
}