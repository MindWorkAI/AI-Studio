using AIStudio.Tools.Media;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Where the transcript of a recording waits while the lab turns it into material, cf. PersistentMediaTranscript.
/// </summary>
/// <remarks>
/// The lab keeps only the filtered text of a transcript, as the text of the material. The transcript itself lies in
/// a folder of its own below media-staging of the data directory, which the lab deletes once it has read the
/// transcript; whatever a crash leaves there, TranscriptStagingCleanupService removes with the next start. Backups
/// leave that folder out.
/// </remarks>
/// <param name="dataDirectory">The data directory of AI Studio.</param>
internal sealed class BflTranscriptLocation(string dataDirectory) : IMediaTranscriptLocation
{
    /// <summary>
    /// The folder of the data directory for transcripts on their way, shared with the chat; see TranscriptStagingCleanupService.
    /// </summary>
    private const string STAGING_DIRECTORY_NAME = "media-staging";

    /// <summary>
    /// The name of the transcript in its folder.
    /// </summary>
    private const string TRANSCRIPT_FILE_NAME = "transcript.md";

    /// <summary>
    /// Gets the folder of this transcript, which no other one shares.
    /// </summary>
    public string FolderPath { get; } = Path.Combine(dataDirectory, STAGING_DIRECTORY_NAME, $"bfl-{Guid.NewGuid():N}");

    /// <inheritdoc />
    public Task<MediaTranscriptSlot> ResolveAsync(string mediaPath, CancellationToken token)
    {
        var path = Path.Combine(this.FolderPath, TRANSCRIPT_FILE_NAME);
        return Task.FromResult(new MediaTranscriptSlot(path, path));
    }

    /// <summary>
    /// Deletes the transcript with its folder, without failing.
    /// </summary>
    /// <returns>False when the folder could not be deleted; the next start removes it.</returns>
    public bool TryDelete()
    {
        try
        {
            if (Directory.Exists(this.FolderPath))
                Directory.Delete(this.FolderPath, true);

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}