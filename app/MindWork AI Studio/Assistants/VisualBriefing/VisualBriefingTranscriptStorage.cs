using AIStudio.Chat;
using AIStudio.Tools.Media;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines <c>VisualBriefingTranscriptStorage</c> for the visual briefing feature.
/// </summary>
public sealed class VisualBriefingTranscriptStorage(VisualBriefingStore store) : IMediaTranscriptStorage
{
    /// <summary>
    /// Defines <c>CanStore</c> for the visual briefing feature.
    /// </summary>
    public bool CanStore(MediaImportOwner owner) =>
        owner.Kind is MediaImportOwnerKind.VISUAL_BRIEFING &&
        Guid.TryParse(owner.Id, out _);

    /// <summary>
    /// Defines <c>StoreAsync</c> for the visual briefing feature.
    /// </summary>
    public async Task<FileAttachment> StoreAsync(
        MediaImportTarget target,
        string originalMediaPath,
        string transcript,
        CancellationToken token)
    {
        if (!Guid.TryParse(target.Owner.Id, out var briefingId))
            throw new InvalidDataException("The visual briefing media owner is invalid.");

        var sourceId = await store.FindSourceIdByPathAsync(briefingId, originalMediaPath, token)
            ?? throw new InvalidDataException("The visual briefing media source is not registered.");
        await store.SetTranscriptCurrentAsync(briefingId, sourceId, transcript, token);
        var transcriptPath = store.GetTranscriptPath(briefingId, sourceId);
        return FileAttachment.FromPath(transcriptPath);
    }
}