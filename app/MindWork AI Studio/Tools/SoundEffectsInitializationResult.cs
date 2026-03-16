namespace AIStudio.Tools;

public sealed class SoundEffectsInitializationResult
{
    public bool Success { get; init; }

    public string[] FailedPaths { get; init; } = [];

    public string? ErrorMessage { get; init; }
}