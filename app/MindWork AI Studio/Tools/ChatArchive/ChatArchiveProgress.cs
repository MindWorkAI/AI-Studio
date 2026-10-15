namespace AIStudio.Tools.ChatArchive;

/// <summary>
/// Reports the progress of an export or import operation.
/// </summary>
/// <param name="ProcessedChats">The number of chats processed so far.</param>
/// <param name="TotalChats">The total number of chats to process.</param>
public readonly record struct ChatArchiveProgress(int ProcessedChats, int TotalChats);