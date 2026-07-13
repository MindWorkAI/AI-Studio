using AIStudio.Tools.Services;

namespace AIStudio.Components;

public partial class MediaTranscriptionStatus
{
    private string StatusText => this.MediaTranscriptionService.Phase switch
    {
        MediaTranscriptionPhase.PROBING => $"{this.T("Inspecting media")}: {this.MediaTranscriptionService.CurrentFileName}",
        MediaTranscriptionPhase.TRANSCODING => $"{this.T("Preparing audio")}: {this.MediaTranscriptionService.CurrentFileName}",
        MediaTranscriptionPhase.UPLOADING => $"{this.T("Transcribing")}: {this.MediaTranscriptionService.CurrentFileName}",
        
        _ => this.MediaTranscriptionService.CurrentFileName,
    };

    protected override async Task OnInitializedAsync()
    {
        this.MediaTranscriptionService.StateChanged += this.OnStateChanged;
        await base.OnInitializedAsync();
    }

    private void OnStateChanged() => _ = this.InvokeAsync(this.StateHasChanged);

    protected override void DisposeResources()
    {
        this.MediaTranscriptionService.StateChanged -= this.OnStateChanged;
    }
}