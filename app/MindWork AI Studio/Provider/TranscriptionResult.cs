namespace AIStudio.Provider;

/// <summary>
/// What a transcription provider answered.
/// </summary>
/// <param name="Success">Whether the provider returned a transcript.</param>
/// <param name="Text">The transcript.</param>
/// <param name="ErrorMessage">Why the transcription failed, for the user, when it is known.</param>
/// <param name="FileRejected">Whether the provider refused the media file itself, so that it would refuse the same file again.</param>
public sealed record TranscriptionResult(bool Success, string Text, string ErrorMessage = "", bool FileRejected = false)
{
    public static TranscriptionResult FromText(string text) => new(true, text);

    public static TranscriptionResult Failure(string errorMessage = "") => new(false, string.Empty, errorMessage);

    public static TranscriptionResult Rejection(string errorMessage) => new(false, string.Empty, errorMessage, true);
}