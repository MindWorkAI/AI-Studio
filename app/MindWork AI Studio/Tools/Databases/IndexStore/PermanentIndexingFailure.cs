namespace AIStudio.Tools.Databases.IndexStore;

public sealed record PermanentIndexingFailure(string ParentFileId, string AbsolutePath, string Fingerprint, FileExtractionErrorCode Code, string Message, DateTimeOffset OccurredAtUtc);