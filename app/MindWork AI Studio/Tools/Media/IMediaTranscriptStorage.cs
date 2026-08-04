using AIStudio.Chat;

namespace AIStudio.Tools.Media;

/// <summary>
/// Persists a completed media transcript for a feature-specific owner.
/// </summary>
public interface IMediaTranscriptStorage
{
    /// <summary>
    /// Determines whether this storage handles the specified media-import owner.
    /// </summary>
    /// <param name="owner">The feature-specific media-import owner.</param>
    /// <returns><see langword="true"/> when the transcript can be stored.</returns>
    bool CanStore(MediaImportOwner owner);

    /// <summary>
    /// Persists a completed transcript and returns the attachment exposed to the calling workflow.
    /// </summary>
    /// <param name="target">The stable media-import target.</param>
    /// <param name="originalMediaPath">The original media path.</param>
    /// <param name="transcript">The completed transcript text.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The attachment representing the persisted transcript.</returns>
    Task<FileAttachment> StoreAsync(
        MediaImportTarget target,
        string originalMediaPath,
        string transcript,
        CancellationToken token);
}