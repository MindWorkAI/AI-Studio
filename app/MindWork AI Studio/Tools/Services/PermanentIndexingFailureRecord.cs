namespace AIStudio.Tools.Services;

public sealed record PermanentIndexingFailureRecord(string Fingerprint, FileExtractionErrorCode Code, string Message, DateTimeOffset OccurredAtUtc);