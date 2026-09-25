namespace AIStudio.Tools.ChatArchive;

/// <summary>
/// Finds the files AI Studio keeps inside a chat directory itself, such as transcripts.
/// </summary>
/// <remarks>
/// Documents the user attaches are never copied into a chat directory: they stay where the
/// user put them and the chat only stores their path. Everything below a chat directory was
/// therefore written by AI Studio, which makes the directory contents a reliable definition
/// of "the app's own files" — minus the two files the chat storage owns itself.
/// </remarks>
public static class ChatArchiveChatFiles
{
    /// <summary>
    /// Collects the app-owned files of one chat.
    /// </summary>
    /// <param name="chatDirectory">The directory of the chat.</param>
    /// <returns>The absolute paths of the files, keyed by their chat-relative path.</returns>
    public static Dictionary<string, string> Collect(string chatDirectory)
    {
        var files = new Dictionary<string, string>(PathTools.COMPARER);
        if (!Directory.Exists(chatDirectory))
            return files;

        foreach (var filePath in Directory.EnumerateFiles(chatDirectory, "*", SearchOption.AllDirectories))
        {
            // Skip the temporary files the chat storage writes while saving a chat:
            var fileName = Path.GetFileName(filePath);
            if (fileName.StartsWith('.') && fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                continue;

            var chatRelativePath = Path.GetRelativePath(chatDirectory, filePath).Replace(Path.DirectorySeparatorChar, ChatArchiveFormat.ENTRY_SEPARATOR);

            // The chat itself is always exported and is not one of its own attachments:
            if (chatRelativePath.Equals(ChatArchiveFormat.THREAD_FILE_NAME, StringComparison.OrdinalIgnoreCase) ||
                chatRelativePath.Equals(ChatArchiveFormat.NAME_FILE_NAME, StringComparison.OrdinalIgnoreCase))
                continue;

            files[chatRelativePath] = filePath;
        }

        return files;
    }

    /// <summary>
    /// Determines whether at least one of the given chats owns a file.
    /// </summary>
    /// <remarks>
    /// This stops at the first file found, so that asking the question stays cheap even for
    /// a large chat storage.
    /// </remarks>
    /// <param name="chatDirectories">The directories of the chats to check.</param>
    /// <param name="token">Cancels the search.</param>
    /// <returns>Whether any app-owned file exists.</returns>
    public static bool HasAny(IEnumerable<string> chatDirectories, CancellationToken token)
    {
        foreach (var chatDirectory in chatDirectories)
        {
            token.ThrowIfCancellationRequested();

            if (Collect(chatDirectory).Count > 0)
                return true;
        }

        return false;
    }
}
