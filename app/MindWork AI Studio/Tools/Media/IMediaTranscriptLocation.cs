namespace AIStudio.Tools.Media;

/// <summary>
/// Where the persistent transcript of an audio or video file lives, cf. PersistentMediaTranscript.
/// </summary>
/// <remarks>
/// A location only names the place. Reading and writing the transcript is left to
/// PersistentMediaTranscript, so every location keeps the same rules against paying twice.
/// </remarks>
internal interface IMediaTranscriptLocation
{
    /// <summary>
    /// Finds the place of the transcript of a media file.
    /// </summary>
    /// <param name="mediaPath">The audio or video file.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The place of the transcript, whether or not a transcript is there yet.</returns>
    Task<MediaTranscriptSlot> ResolveAsync(string mediaPath, CancellationToken token);
}