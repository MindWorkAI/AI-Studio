using System.Text;

using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Media;

/// <summary>
/// Reads or creates the persistent Markdown transcript of an audio or video file.
/// </summary>
/// <remarks>
/// Every transcript which AI Studio keeps comes from here, so the rules which keep a paid
/// transcription from being paid twice hold for every caller: an existing transcript is used as it
/// is, a transcript which arrived is stored even when the caller cancelled meanwhile, and nothing is
/// stored for a file which changed during its transcription or for a transcription without text.
/// Where a transcript lives is up to the location, e.g. next to the media file or below the data
/// directory.
/// </remarks>
internal static class PersistentMediaTranscript
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PersistentMediaTranscript).Namespace, nameof(PersistentMediaTranscript));

    /// <summary>
    /// Whether a file is an audio or video file, which holds no text until it is transcribed.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>True for audio and video files.</returns>
    public static bool IsMediaPath(string path) => FileTypes.IsAllowedPath(path, FileTypes.AUDIO, FileTypes.VIDEO);

    /// <summary>
    /// Returns where the transcript of a media file is, transcribing the file only when its location holds no transcript yet.
    /// </summary>
    /// <param name="location">Where the transcript lives.</param>
    /// <param name="mediaPath">The audio or video file.</param>
    /// <param name="provider">The transcription provider the caller allows, or null when no provider may hear the file.</param>
    /// <param name="transcribe">Transcribes the media file with the given provider, usually MediaTranscriptionService.TranscribeAsync.</param>
    /// <param name="logger">The logger of the caller.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>Where the transcript is, or why there is none.</returns>
    public static async Task<PersistentMediaTranscriptResult> ReadOrCreateAsync(IMediaTranscriptLocation location, string mediaPath, TranscriptionProvider? provider, Func<string, TranscriptionProvider, CancellationToken, Task<MediaTranscriptionResult>> transcribe, ILogger logger, CancellationToken token)
    {
        //
        // The stamp comes first: a location may read the whole file to find the place of its
        // transcript, and a change from then on must keep a transcript out of that place.
        //
        FileStamp sourceBefore;
        MediaTranscriptSlot slot;
        try
        {
            sourceBefore = FileStamp.Read(mediaPath);
            slot = await location.ResolveAsync(mediaPath, token);
        }
        catch (OperationCanceledException)
        {
            return PersistentMediaTranscriptResult.Cancelled();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not inspect the media file '{MediaFilePath}' before its transcription.", mediaPath);
            return PersistentMediaTranscriptResult.Unavailable(PersistentMediaTranscriptStatus.SOURCE_UNREADABLE, null, TB("The media file could not be read."));
        }

        try
        {
            if (await HoldsTextAsync(slot.Path, token))
            {
                logger.LogInformation("Reusing the transcript '{TranscriptFilePath}' of the media file '{MediaFilePath}'.", slot.Path, mediaPath);
                return PersistentMediaTranscriptResult.Ready(slot, reused: true);
            }
        }
        catch (OperationCanceledException)
        {
            return PersistentMediaTranscriptResult.Cancelled();
        }
        catch (Exception exception)
        {
            //
            // Transcribing again would mean paying for a transcript which exists, and storing it
            // would most likely fail for the same reason:
            //
            logger.LogWarning(exception, "Could not read the transcript '{TranscriptFilePath}' of the media file '{MediaFilePath}'.", slot.Path, mediaPath);
            return PersistentMediaTranscriptResult.Unavailable(PersistentMediaTranscriptStatus.TRANSCRIPT_UNREADABLE, slot, string.Format(TB("Was not able to read the existing transcript: {0}"), exception.Message));
        }

        if (provider is null)
            return PersistentMediaTranscriptResult.Unavailable(PersistentMediaTranscriptStatus.NO_PROVIDER, slot, TB("No usable transcription provider is configured."));

        MediaTranscriptionResult transcription;
        try
        {
            transcription = await transcribe(mediaPath, provider, token);
        }
        catch (OperationCanceledException)
        {
            return PersistentMediaTranscriptResult.Cancelled();
        }

        switch (transcription.Status)
        {
            case MediaTranscriptionResultStatus.CANCELLED:
                return PersistentMediaTranscriptResult.Cancelled();

            case MediaTranscriptionResultStatus.NO_AUDIBLE_SIGNAL:
                return PersistentMediaTranscriptResult.Unavailable(PersistentMediaTranscriptStatus.NO_AUDIBLE_SIGNAL, slot, transcription.UserMessage);

            case MediaTranscriptionResultStatus.FAILED when transcription.FileRejected:
                return PersistentMediaTranscriptResult.Unavailable(PersistentMediaTranscriptStatus.REJECTED, slot, transcription.UserMessage);

            case MediaTranscriptionResultStatus.FAILED:
                return PersistentMediaTranscriptResult.Unavailable(PersistentMediaTranscriptStatus.TRANSCRIPTION_FAILED, slot, transcription.UserMessage, transcription.ErrorCode);
        }

        if (string.IsNullOrWhiteSpace(transcription.Text))
            return PersistentMediaTranscriptResult.Unavailable(PersistentMediaTranscriptStatus.EMPTY, slot, TB("The transcription provider returned an empty transcript."));

        if (!sourceBefore.StillMatches(mediaPath))
            return SourceChanged(slot);

        var temporaryPath = $"{slot.Path}.{Guid.NewGuid():N}.tmp";
        try
        {
            var directory = Path.GetDirectoryName(slot.Path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            //
            // The transcription is paid for at this point. Storing it is quick, so it is completed
            // even when the caller cancelled meanwhile, and the next attempt can use it:
            //
            await File.WriteAllTextAsync(temporaryPath, transcription.Text, new UTF8Encoding(false), CancellationToken.None);
            if (!sourceBefore.StillMatches(mediaPath))
                return SourceChanged(slot);

            File.Move(temporaryPath, slot.Path, true);
            logger.LogInformation("Stored the transcript '{TranscriptFilePath}' of the media file '{MediaFilePath}'.", slot.Path, mediaPath);
            return PersistentMediaTranscriptResult.Ready(slot, reused: false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not store the transcript '{TranscriptFilePath}' of the media file '{MediaFilePath}'.", slot.Path, mediaPath);
            return PersistentMediaTranscriptResult.Unavailable(PersistentMediaTranscriptStatus.STORE_FAILED, slot, string.Format(TB("Was not able to store the transcript: {0}"), exception.Message));
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
                logger.LogWarning(exception, "Could not remove the temporary transcript '{TempFilePath}'.", temporaryPath);
            }
        }
    }

    private static PersistentMediaTranscriptResult SourceChanged(MediaTranscriptSlot slot) => PersistentMediaTranscriptResult.Unavailable(PersistentMediaTranscriptStatus.SOURCE_CHANGED, slot, TB("The media file changed while it was being transcribed, so its transcript was not kept."));

    /// <summary>
    /// Whether a transcript exists and holds more than white space.
    /// </summary>
    /// <remarks>
    /// Reads only as far as the first character which is not white space, so a large file is
    /// never loaded as a whole.
    /// </remarks>
    private static async Task<bool> HoldsTextAsync(string path, CancellationToken token)
    {
        if (!File.Exists(path))
            return false;

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[1024];
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), token)) > 0)
        {
            for (var index = 0; index < read; index++)
                if (!char.IsWhiteSpace(buffer[index]))
                    return true;
        }

        return false;
    }

    /// <summary>
    /// The size and write time of a media file, to notice a change during its transcription.
    /// </summary>
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