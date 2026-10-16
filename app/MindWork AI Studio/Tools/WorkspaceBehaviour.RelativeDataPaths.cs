using System.Text;
using System.Text.Json;

using AIStudio.Chat;

namespace AIStudio.Tools;

//
// Compatibility shim, see documentation/compatibility-shims/2026-10-relative-data-paths.md.
// Remove after 2027-04-14.
//
public static partial class WorkspaceBehaviour
{
    /// <summary>
    /// Writes every stored chat once more, so that the paths of its files below the data directory become relative.
    /// </summary>
    /// <remarks>
    /// A chat stored before carries absolute paths. They still work on this computer, but a backup of
    /// the data directory brings them to another one, where they lead nowhere. Each chat is read and
    /// written within one hold of its lock, so that nothing AI Studio stores meanwhile gets lost. The
    /// time of the last change stays as it was: the workspaces sort their chats by it, and the
    /// maintenance of the temporary chats deletes them by it.
    /// </remarks>
    /// <param name="token">Cancels the pass, e.g., when AI Studio shuts down.</param>
    /// <returns>Whether every chat was taken care of; false when one has to wait for the next start.</returns>
    internal static async Task<bool> StoreChatsWithRelativePathsAsync(CancellationToken token)
    {
        var everyChatStored = true;
        foreach (var chat in EnumerateStoredChats())
        {
            token.ThrowIfCancellationRequested();
            if (!await StoreChatWithRelativePathsAsync(chat, token))
                everyChatStored = false;
        }

        return everyChatStored;
    }

    private static IEnumerable<LoadChat> EnumerateStoredChats()
    {
        if (Directory.Exists(TemporaryChatsRootDirectory))
            foreach (var chatDirectory in Directory.EnumerateDirectories(TemporaryChatsRootDirectory))
                if (Guid.TryParse(Path.GetFileName(chatDirectory), out var chatId))
                    yield return new(Guid.Empty, chatId);

        if (!Directory.Exists(WorkspaceRootDirectory))
            yield break;

        foreach (var workspaceDirectory in Directory.EnumerateDirectories(WorkspaceRootDirectory))
        {
            if (!Guid.TryParse(Path.GetFileName(workspaceDirectory), out var workspaceId))
                continue;

            foreach (var chatDirectory in Directory.EnumerateDirectories(workspaceDirectory))
                if (Guid.TryParse(Path.GetFileName(chatDirectory), out var chatId))
                    yield return new(workspaceId, chatId);
        }
    }

    /// <returns>Whether the chat was taken care of: written anew, or with nothing to change.</returns>
    private static async Task<bool> StoreChatWithRelativePathsAsync(LoadChat chat, CancellationToken token)
    {
        var (acquired, semaphore) = await TryAcquireChatSemaphoreAsync(chat.WorkspaceId, chat.ChatId, nameof(StoreChatWithRelativePathsAsync));
        if (!acquired)
            return false;

        try
        {
            var chatDirectory = GetChatDirectory(chat.WorkspaceId, chat.ChatId);
            var chatPath = Path.Join(chatDirectory, "thread.json");
            if (!File.Exists(chatPath))
                return true;

            var storedJson = await File.ReadAllTextAsync(chatPath, Encoding.UTF8, token);
            var thread = JsonSerializer.Deserialize<ChatThread>(storedJson, JSON_OPTIONS);
            if (thread is null)
                return true;

            var json = JsonSerializer.Serialize(thread, JSON_OPTIONS);
            if (json == storedJson)
                return true;

            var lastEditTime = File.GetLastWriteTimeUtc(chatPath);
            var temporaryPath = Path.Join(chatDirectory, $".thread-{Guid.NewGuid():N}.tmp");
            try
            {
                await File.WriteAllTextAsync(temporaryPath, json, Encoding.UTF8, token);
                File.Move(temporaryPath, chatPath, overwrite: true);
                File.SetLastWriteTimeUtc(chatPath, lastEditTime);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }

            return true;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            // Opening the chat fails the same way, so there is nothing to store, now or at the next start:
            LOG.LogWarning(exception, "The chat '{ChatId}' of workspace '{WorkspaceId}' could not be read, so its paths stay as they are.", chat.ChatId, chat.WorkspaceId);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LOG.LogWarning(exception, "The chat '{ChatId}' of workspace '{WorkspaceId}' could not be stored with relative paths. The next start tries again.", chat.ChatId, chat.WorkspaceId);
            return false;
        }
        finally
        {
            semaphore.Release();
        }
    }
}