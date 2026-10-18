namespace AIStudio.Tools.Media;

/// <summary>
/// How the request for a persistent transcript ended, cf. PersistentMediaTranscript.
/// </summary>
internal enum PersistentMediaTranscriptStatus
{
    /// <summary>
    /// The transcript is stored at its place and holds text.
    /// </summary>
    READY,

    /// <summary>
    /// There is no transcript yet, and the caller allows no transcription provider.
    /// </summary>
    NO_PROVIDER,

    /// <summary>
    /// The media file could not be read before it was transcribed.
    /// </summary>
    SOURCE_UNREADABLE,

    /// <summary>
    /// A transcript exists, but it could not be read.
    /// </summary>
    TRANSCRIPT_UNREADABLE,

    /// <summary>
    /// The transcription failed. The error code of the media pipeline tells why, when it is known.
    /// </summary>
    TRANSCRIPTION_FAILED,

    /// <summary>
    /// The media file holds no audible signal, so there was nothing to transcribe.
    /// </summary>
    NO_AUDIBLE_SIGNAL,

    /// <summary>
    /// The transcription provider returned no text. Nothing was stored.
    /// </summary>
    EMPTY,

    /// <summary>
    /// The media file changed while it was transcribed. Nothing was stored.
    /// </summary>
    SOURCE_CHANGED,

    /// <summary>
    /// The transcript could not be stored at its place.
    /// </summary>
    STORE_FAILED,

    /// <summary>
    /// The caller cancelled before a transcript was available.
    /// </summary>
    CANCELLED,
}