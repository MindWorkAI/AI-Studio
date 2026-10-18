namespace AIStudio.Tools.Media;

/// <summary>
/// Keeps the transcript of a media file next to it, as '&lt;media file&gt;.transcript.md'.
/// </summary>
/// <remarks>
/// The Batch Processing assistant writes its results into the folder the user chose, and its
/// transcripts belong there as well, where people find and reuse them. Data sources must not write
/// into the folders of the user and keep their transcripts below the data directory instead.
/// </remarks>
internal sealed class MediaSidecarTranscripts : IMediaTranscriptLocation
{
    public const string FILE_SUFFIX = ".transcript.md";

    public static readonly MediaSidecarTranscripts INSTANCE = new();

    /// <inheritdoc />
    public Task<MediaTranscriptSlot> ResolveAsync(string mediaPath, CancellationToken token)
    {
        var path = GetFilePath(mediaPath);
        return Task.FromResult(new MediaTranscriptSlot(path, path));
    }

    /// <summary>
    /// The path of the transcript next to a media file.
    /// </summary>
    /// <param name="mediaPath">The audio or video file.</param>
    /// <returns>The path of its transcript.</returns>
    public static string GetFilePath(string mediaPath) => mediaPath + FILE_SUFFIX;

    /// <summary>
    /// Whether a file is a transcript next to a media file, or a temporary file left while one was stored.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>True for transcripts and their temporary files.</returns>
    public static bool IsArtifactPath(string path)
    {
        var fileName = Path.GetFileName(path);
        if (fileName.EndsWith(FILE_SUFFIX, StringComparison.OrdinalIgnoreCase))
            return true;

        var suffixIndex = fileName.LastIndexOf(FILE_SUFFIX + ".", StringComparison.OrdinalIgnoreCase);
        return suffixIndex >= 0 && fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether a media file has a transcript next to it, so no transcription provider is needed for it.
    /// </summary>
    /// <param name="mediaPath">The audio or video file.</param>
    /// <returns>True when a transcript which is not empty exists.</returns>
    public static bool HasReusableTranscript(string mediaPath)
    {
        var transcriptFilePath = GetFilePath(mediaPath);
        try
        {
            return File.Exists(transcriptFilePath) && new FileInfo(transcriptFilePath).Length > 0;
        }
        catch
        {
            // The concrete read error is reported when the affected file is
            // processed. Here we only decide whether a provider is required.
            return File.Exists(transcriptFilePath);
        }
    }
}