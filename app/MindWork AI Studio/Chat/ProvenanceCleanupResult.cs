namespace AIStudio.Chat;

/// <summary>
/// What a cleanup of the provenance store did, see ProvenanceCleanup.
/// </summary>
/// <param name="Orphaned">How many entries of chats which no longer exist were removed.</param>
/// <param name="Unreferenced">How many entries their chats no longer refer to were removed.</param>
/// <param name="Skipped">How many chats were left unread, since nothing changed in them since the last cleanup.</param>
/// <param name="Unreadable">How many chats could not be read, and thus kept all their entries.</param>
public sealed record ProvenanceCleanupResult(int Orphaned, int Unreferenced, int Skipped, int Unreadable);