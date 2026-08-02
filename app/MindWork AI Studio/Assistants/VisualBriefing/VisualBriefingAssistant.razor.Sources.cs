using AIStudio.Chat;
using AIStudio.Dialogs;
using AIStudio.Tools.Media;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Assistants.VisualBriefing;

public partial class VisualBriefingAssistant
{
    /// <summary>
    /// Defines <c>CurrentMediaOwner</c> for the visual briefing feature.
    /// </summary>
    private MediaImportOwner CurrentMediaOwner => this.selectedBriefing is null
        ? new(MediaImportOwnerKind.VISUAL_BRIEFING, Guid.Empty.ToString("D"))
        : MediaImportOwner.ForVisualBriefing(this.selectedBriefing.BriefingId);

    /// <summary>
    /// Defines <c>SourceMaterialChangedAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task SourceMaterialChangedAsync(HashSet<FileAttachment> _)
    {
        var visualPaths = this.editor.VisualAssets.Select(attachment => attachment.FilePath).ToHashSet(PathComparer());
        this.editor.SourceMaterial.RemoveWhere(attachment => visualPaths.Contains(attachment.FilePath));
        await this.SaveCurrentAsync(reload: true);
    }

    /// <summary>
    /// Defines <c>VisualAssetsChangedAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task VisualAssetsChangedAsync(HashSet<FileAttachment> _)
    {
        var visualPaths = this.editor.VisualAssets.Select(attachment => attachment.FilePath).ToHashSet(PathComparer());
        this.editor.SourceMaterial.RemoveWhere(attachment => visualPaths.Contains(attachment.FilePath));
        await this.SaveCurrentAsync(reload: true);
    }

    /// <summary>
    /// Defines <c>RefreshSourceStatusAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task RefreshSourceStatusAsync()
    {
        if (this.selectedBriefing is null)
            return;

        var latest = await this.Store.LoadAsync(this.selectedBriefing.BriefingId);
        if (latest is null)
            return;

        this.selectedBriefing.Sources = latest.Sources;
        this.StateHasChanged();
    }

    /// <summary>
    /// Defines <c>MonitorSourceStatusAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task MonitorSourceStatusAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try
        {
            while (await timer.WaitForNextTickAsync(token))
                if (this.selectedBriefing is not null && !this.IsCurrentBusy)
                    await this.InvokeAsync(this.RefreshSourceStatusAsync);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    /// <summary>
    /// Defines <c>RelinkAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task RelinkAsync(VisualBriefingSource source)
    {
        if (this.selectedBriefing is null)
            return;
        
        var response = await this.RustService.SelectFile(T("Relink briefing source"), initialFile: source.Path);
        if (response.UserCancelled)
            return;

        await this.Store.RelinkSourceAsync(this.selectedBriefing.BriefingId, source.SourceId, response.SelectedFilePath);
        await this.ReloadListAsync(this.selectedBriefing.BriefingId);
    }

    /// <summary>
    /// Defines <c>RemoveSourceAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task RemoveSourceAsync(VisualBriefingSource source)
    {
        if (this.selectedBriefing is null)
            return;

        await this.Store.RemoveSourceAsync(this.selectedBriefing.BriefingId, source.SourceId);
        await this.ReloadListAsync(this.selectedBriefing.BriefingId);
    }

    /// <summary>
    /// Defines <c>RetranscribeAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task RetranscribeAsync(VisualBriefingSource source)
    {
        if (this.selectedBriefing is null || !source.IsMedia || !File.Exists(source.Path))
            return;

        var parameters = new DialogParameters<ConfirmDialog>
        {
            { dialog => dialog.Message, T("The media file changed. Transcribe it again with the configured transcription provider?") },
        };

        var reference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Transcribe media again"), parameters, DialogOptions.FULLSCREEN);
        var result = await reference.Result;
        if (result is null || result.Canceled)
            return;

        this.MediaTranscriptionService.TryStartAttachmentBatch(
            [source.Path],
            new(this.CurrentMediaOwner, source.SourceId.ToString("D")));
    }

    /// <summary>
    /// Defines <c>MediaStateChanged</c> for the visual briefing feature.
    /// </summary>
    private void MediaStateChanged(MediaImportOwner owner)
    {
        if (owner.Kind is not MediaImportOwnerKind.VISUAL_BRIEFING ||
            !Guid.TryParse(owner.Id, out var briefingId))
            return;

        _ = this.InvokeAsync(async () =>
        {
            if (!this.MediaTranscriptionService.IsBusy(owner))
            {
                var latest = await this.Store.LoadAsync(briefingId);
                if (latest is not null)
                {
                    this.UpdateProject(latest);

                    if (this.selectedBriefing?.BriefingId == briefingId)
                        await this.ApplySelectedBriefingAsync(latest);
                }
            }

            this.StateHasChanged();
        });
    }

    /// <summary>
    /// Defines <c>SourceStatusName</c> for the visual briefing feature.
    /// </summary>
    private string SourceStatusName(VisualBriefingSourceStatus status) => status switch
    {
        VisualBriefingSourceStatus.UNCHANGED => T("unchanged"),
        VisualBriefingSourceStatus.CHANGED => T("changed"),
        VisualBriefingSourceStatus.TRANSCRIPT_OUTDATED => T("transcript outdated"),
        VisualBriefingSourceStatus.UNREACHABLE => T("unreachable"),

        _ => status.ToString(),
    };

    /// <summary>
    /// Defines <c>SourceStatusColor</c> for the visual briefing feature.
    /// </summary>
    private static Color SourceStatusColor(VisualBriefingSourceStatus status) => status switch
    {
        VisualBriefingSourceStatus.UNCHANGED => Color.Success,
        VisualBriefingSourceStatus.CHANGED => Color.Warning,
        VisualBriefingSourceStatus.TRANSCRIPT_OUTDATED => Color.Warning,
        VisualBriefingSourceStatus.UNREACHABLE => Color.Error,
        _ => Color.Default,
    };
}