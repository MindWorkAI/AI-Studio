using System.Text;

using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

namespace AIStudio.Tools.Media;

/// <summary>
/// Reads and creates the persistent Markdown transcript stored next to an audio or video file.
/// </summary>
internal static class PersistentMediaTranscript
{
    public const string FILE_SUFFIX = ".transcript.md";

    internal enum Reuse
    {
        ANY,
        CURRENT_SOURCE,
        NEVER,
    }

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PersistentMediaTranscript).Namespace, nameof(PersistentMediaTranscript));

    public static bool IsMediaPath(string path) => FileTypes.IsAllowedPath(path, FileTypes.AUDIO, FileTypes.VIDEO);

    public static string GetFilePath(string mediaPath) => mediaPath + FILE_SUFFIX;

    public static bool IsArtifactPath(string path)
    {
        var fileName = Path.GetFileName(path);
        if (fileName.EndsWith(FILE_SUFFIX, StringComparison.OrdinalIgnoreCase))
            return true;

        var suffixIndex = fileName.LastIndexOf(FILE_SUFFIX + ".", StringComparison.OrdinalIgnoreCase);
        return suffixIndex >= 0 && fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether a generated transcript belongs to a media file selected by the same patterns.
    /// Such a transcript is an implementation detail, not a second document.
    /// </summary>
    public static bool IsCoveredArtifact(string path, IReadOnlyList<string> patterns)
    {
        if (!TryGetMediaPath(path, out var mediaPath))
            return false;

        // An orphan left behind after its media file was moved or deleted is still generated data,
        // not a new user document. When the media remains, the transcript is hidden only if that
        // media is selected; this still lets a deliberate *.md-only source index the sidecar.
        return !File.Exists(mediaPath)
               || IsMediaPath(mediaPath) && FileNamePatterns.Matches(mediaPath, patterns);
    }

    public static bool IsReusable(string mediaPath, Reuse reuse)
    {
        if (reuse is Reuse.NEVER)
            return false;

        try
        {
            var transcript = new FileInfo(GetFilePath(mediaPath));
            if (!transcript.Exists || transcript.Length is 0)
                return false;

            if (reuse is Reuse.ANY)
                return true;

            var media = new FileInfo(mediaPath);
            return media.Exists && transcript.LastWriteTimeUtc == media.LastWriteTimeUtc;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<MediaTranscriptionResult> ReadOrCreateAsync(
        MediaTranscriptionService transcriptionService,
        string mediaPath,
        Reuse reuse,
        ILogger logger,
        CancellationToken token)
    {
        var transcriptPath = GetFilePath(mediaPath);
        if (IsReusable(mediaPath, reuse))
        {
            try
            {
                var existingTranscript = await File.ReadAllTextAsync(transcriptPath, token);
                if (!string.IsNullOrWhiteSpace(existingTranscript))
                {
                    logger.LogInformation("Reusing persistent transcript '{TranscriptFilePath}' for media file '{MediaFilePath}'.", transcriptPath, mediaPath);
                    return MediaTranscriptionResult.Succeeded(existingTranscript);
                }

                logger.LogWarning("The persistent transcript '{TranscriptFilePath}' for media file '{MediaFilePath}' is empty and will be replaced.", transcriptPath, mediaPath);
            }
            catch (OperationCanceledException)
            {
                return MediaTranscriptionResult.Cancelled();
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not read persistent transcript '{TranscriptFilePath}' for media file '{MediaFilePath}'.", transcriptPath, mediaPath);
                return MediaTranscriptionResult.Failed(string.Format(TB("Was not able to read the existing transcript: {0}"), exception.Message));
            }
        }

        if (!transcriptionService.HasUsableTranscriptionProvider)
            return MediaTranscriptionResult.Failed(TB("No usable transcription provider is configured."));

        if (reuse is Reuse.NEVER)
        {
            try
            {
                File.Delete(transcriptPath);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not remove stale transcript '{TranscriptFilePath}' before retranscribing media file '{MediaFilePath}'.", transcriptPath, mediaPath);
                return MediaTranscriptionResult.Failed(string.Format(TB("Was not able to replace the outdated transcript next to the media file: {0}"), exception.Message));
            }
        }

        FileStamp sourceBefore;
        try
        {
            sourceBefore = FileStamp.Read(mediaPath);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not inspect media file '{MediaFilePath}' before transcription.", mediaPath);
            return MediaTranscriptionResult.Failed(TB("The media file could not be read."));
        }

        var transcription = await transcriptionService.TranscribeAsync(mediaPath, token);
        if (transcription.Status is not MediaTranscriptionResultStatus.SUCCEEDED)
            return transcription;

        if (string.IsNullOrWhiteSpace(transcription.Text))
            return MediaTranscriptionResult.Failed(TB("The transcription provider returned an empty transcript."));

        if (!sourceBefore.StillMatches(mediaPath))
            return MediaTranscriptionResult.Failed(TB("The media file changed while it was being transcribed. It will be tried again."));

        var temporaryPath = transcriptPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, transcription.Text, new UTF8Encoding(false), CancellationToken.None);
            if (!sourceBefore.StillMatches(mediaPath))
                return MediaTranscriptionResult.Failed(TB("The media file changed while it was being transcribed. It will be tried again."));

            File.Move(temporaryPath, transcriptPath, true);
            File.SetLastWriteTimeUtc(transcriptPath, sourceBefore.LastWriteTimeUtc);
            logger.LogInformation("Stored persistent transcript '{TranscriptFilePath}' next to media file '{MediaFilePath}'.", transcriptPath, mediaPath);
            return transcription;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not store persistent transcript '{TranscriptFilePath}' for media file '{MediaFilePath}'.", transcriptPath, mediaPath);
            return MediaTranscriptionResult.Failed(string.Format(TB("Was not able to store the transcript next to the media file: {0}"), exception.Message));
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not remove temporary transcript '{TempFilePath}'.", temporaryPath);
            }
        }
    }

    private static bool TryGetMediaPath(string path, out string mediaPath)
    {
        var suffixIndex = path.LastIndexOf(FILE_SUFFIX, StringComparison.OrdinalIgnoreCase);
        if (suffixIndex < 0)
        {
            mediaPath = string.Empty;
            return false;
        }

        var remainder = path[(suffixIndex + FILE_SUFFIX.Length)..];
        if (remainder.Length > 0 && !(remainder[0] is '.' && remainder.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)))
        {
            mediaPath = string.Empty;
            return false;
        }

        mediaPath = path[..suffixIndex];
        return true;
    }

    private readonly record struct FileStamp(long Length, DateTime LastWriteTimeUtc)
    {
        public static FileStamp Read(string path)
        {
            var file = new FileInfo(path);
            file.Refresh();
            if (!file.Exists)
                throw new FileNotFoundException("The media file does not exist.", path);

            return new(file.Length, file.LastWriteTimeUtc);
        }

        public bool StillMatches(string path)
        {
            try
            {
                return Read(path) == this;
            }
            catch
            {
                return false;
            }
        }
    }
}
