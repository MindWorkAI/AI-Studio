using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Media;

/// <summary>
/// Typed terminal result returned by media import and voice operations.
/// </summary>
/// <param name="Status">Terminal operation status.</param>
/// <param name="Text">Transcript text for a successful operation.</param>
/// <param name="UserMessage">Localized message suitable for display after a warning or failure.</param>
/// <param name="ErrorCode">Optional stable runtime failure category.</param>
public sealed record MediaTranscriptionResult(MediaTranscriptionResultStatus Status, string Text, string UserMessage, MediaJobErrorCode? ErrorCode = null)
{
    /// <summary>Creates a successful result.</summary>
    /// <param name="text">Provider transcript.</param>
    public static MediaTranscriptionResult Succeeded(string text) => new(MediaTranscriptionResultStatus.SUCCEEDED, text, string.Empty);

    /// <summary>Creates a failed result.</summary>
    /// <param name="userMessage">Localized visible message.</param>
    /// <param name="errorCode">Optional runtime error category.</param>
    public static MediaTranscriptionResult Failed(string userMessage, MediaJobErrorCode? errorCode = null) => new(MediaTranscriptionResultStatus.FAILED, string.Empty, userMessage, errorCode);

    /// <summary>Creates a warning result for media without an audible signal.</summary>
    /// <param name="userMessage">Localized visible warning.</param>
    public static MediaTranscriptionResult NoAudibleSignal(string userMessage) => new(
        MediaTranscriptionResultStatus.NO_AUDIBLE_SIGNAL,
        string.Empty,
        userMessage);

    /// <summary>Creates a cancelled result without relying on visible text.</summary>
    public static MediaTranscriptionResult Cancelled() => new(MediaTranscriptionResultStatus.CANCELLED, string.Empty, string.Empty, MediaJobErrorCode.CANCELLED);
}