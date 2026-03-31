namespace AIStudio.Tools;

public sealed class AudioRecordingResult
{
    public string MimeType { get; init; } = string.Empty;

    public bool ChangedMimeType { get; init; }
}