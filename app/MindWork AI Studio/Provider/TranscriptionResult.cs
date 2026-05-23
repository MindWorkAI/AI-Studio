namespace AIStudio.Provider;

public sealed record TranscriptionResult(bool Success, string Text)
{
    public static TranscriptionResult FromText(string text) => new(true, text);

    public static TranscriptionResult Failure() => new(false, string.Empty);
}