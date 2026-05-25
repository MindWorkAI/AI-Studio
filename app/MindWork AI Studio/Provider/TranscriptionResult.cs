namespace AIStudio.Provider;

public sealed record TranscriptionResult(bool Success, string Text, string ErrorMessage = "")
{
    public static TranscriptionResult FromText(string text) => new(true, text);

    public static TranscriptionResult Failure(string errorMessage = "") => new(false, string.Empty, errorMessage);
}