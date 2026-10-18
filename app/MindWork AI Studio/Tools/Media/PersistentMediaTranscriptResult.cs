using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Media;

/// <summary>
/// The outcome of a request for a persistent transcript, cf. PersistentMediaTranscript.
/// </summary>
/// <remarks>
/// The transcript itself is not part of it: whoever needs the text reads the file at
/// TranscriptPath, so a transcript reaches the AI by the same path no matter whether it was just
/// made or stored long ago.
/// </remarks>
/// <param name="Status">How the request ended.</param>
/// <param name="TranscriptPath">The path of the transcript; empty when the place could not be determined.</param>
/// <param name="Key">What identifies the transcript at its location; empty when the place could not be determined.</param>
/// <param name="Reused">Whether an existing transcript was used instead of a new transcription.</param>
/// <param name="UserMessage">A localized explanation for every status but READY and CANCELLED.</param>
/// <param name="ErrorCode">Why the media pipeline failed, for TRANSCRIPTION_FAILED, when it is known.</param>
internal sealed record PersistentMediaTranscriptResult(PersistentMediaTranscriptStatus Status, string TranscriptPath, string Key, bool Reused, string UserMessage, MediaJobErrorCode? ErrorCode = null)
{
    public static PersistentMediaTranscriptResult Ready(MediaTranscriptSlot slot, bool reused) => new(PersistentMediaTranscriptStatus.READY, slot.Path, slot.Key, reused, string.Empty);

    public static PersistentMediaTranscriptResult Cancelled() => new(PersistentMediaTranscriptStatus.CANCELLED, string.Empty, string.Empty, false, string.Empty);

    public static PersistentMediaTranscriptResult Unavailable(PersistentMediaTranscriptStatus status, MediaTranscriptSlot? slot, string userMessage, MediaJobErrorCode? errorCode = null) => new(status, slot?.Path ?? string.Empty, slot?.Key ?? string.Empty, false, userMessage, errorCode);
}