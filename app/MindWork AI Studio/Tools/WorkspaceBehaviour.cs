using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using AIStudio.Chat;
using AIStudio.Dialogs;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools;

public static class WorkspaceBehaviour
{
    private sealed class WorkspaceChatCacheEntry
    {
        public Guid WorkspaceId { get; init; }

        public Guid ChatId { get; init; }

        public string ChatPath { get; init; } = string.Empty;

        public string ChatName { get; set; } = string.Empty;

        public DateTimeOffset LastEditTime { get; set; }

        public bool IsTemporary { get; init; }
    }

    private sealed class WorkspaceCacheEntry
    {
        public Guid WorkspaceId { get; init; }

        public string WorkspacePath { get; init; } = string.Empty;

        public string WorkspaceName { get; set; } = string.Empty;

        public bool ChatsLoaded { get; set; }

        public List<WorkspaceChatCacheEntry> Chats { get; set; } = [];
    }

    private sealed class WorkspaceTreeCacheState
    {
        public Dictionary<Guid, WorkspaceCacheEntry> Workspaces { get; } = [];

        public List<Guid> WorkspaceOrder { get; } = [];

        public List<WorkspaceChatCacheEntry> TemporaryChats { get; set; } = [];

        public bool IsShellLoaded { get; set; }
    }

    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger(nameof(WorkspaceBehaviour));
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> CHAT_STORAGE_SEMAPHORES = new();
    private static readonly SemaphoreSlim WORKSPACE_TREE_CACHE_SEMAPHORE = new(1, 1);
    private static readonly WorkspaceTreeCacheState WORKSPACE_TREE_CACHE = new();

    private static readonly TimeSpan SEMAPHORE_TIMEOUT = TimeSpan.FromSeconds(6);
    private static volatile bool WORKSPACE_TREE_CACHE_INVALIDATED = true;

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(WorkspaceBehaviour).Namespace, nameof(WorkspaceBehaviour));

    public static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper),
        }
    };

    private static readonly TimeSpan PREFETCH_DELAY_DURATION = TimeSpan.FromMilliseconds(45);

    private static string WorkspaceRootDirectory => Path.Join(SettingsManager.DataDirectory, "workspaces");

    private static string TemporaryChatsRootDirectory => Path.Join(SettingsManager.DataDirectory, "tempChats");

    private static SemaphoreSlim GetChatSemaphore(Guid workspaceId, Guid chatId)
    {
        var key = $"{workspaceId}_{chatId}";
        return CHAT_STORAGE_SEMAPHORES.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
    }

    private static async Task<(bool Acquired, SemaphoreSlim Semaphore)> TryAcquireChatSemaphoreAsync(Guid workspaceId, Guid chatId, string callerName)
    {
        var semaphore = GetChatSemaphore(workspaceId, chatId);
        var acquired = await semaphore.WaitAsync(SEMAPHORE_TIMEOUT);

        if (!acquired)
            LOG.LogWarning("Failed to acquire chat storage semaphore within {Timeout} seconds for workspace '{WorkspaceId}', chat '{ChatId}' in method '{CallerName}'. Skipping operation to prevent potential race conditions or deadlocks.",
                SEMAPHORE_TIMEOUT.TotalSeconds,
                workspaceId,
                chatId,
                callerName);

        return (acquired, semaphore);
    }

    private static WorkspaceTreeChat ToPublicChat(WorkspaceChatCacheEntry chat) => new(chat.WorkspaceId, chat.ChatId, chat.ChatPath, chat.ChatName, chat.LastEditTime, chat.IsTemporary);

    private static WorkspaceTreeWorkspace ToPublicWorkspace(WorkspaceCacheEntry workspace) => new(workspace.WorkspaceId,
        workspace.WorkspacePath,
        workspace.WorkspaceName,
        workspace.ChatsLoaded,
        workspace.Chats.Select(ToPublicChat).ToList());

    private static async Task<string> ReadNameOrDefaultAsync(string nameFilePath, string fallbackName)
    {
        try
        {
            if (!File.Exists(nameFilePath))
                return fallbackName;

            var name = await File.ReadAllTextAsync(nameFilePath, Encoding.UTF8);
            return string.IsNullOrWhiteSpace(name) ? fallbackName : name;
        }
        catch
        {
            return fallbackName;
        }
    }

    private static async Task<List<WorkspaceChatCacheEntry>> ReadWorkspaceChatsCoreAsync(Guid workspaceId, string workspacePath)
    {
        var chats = new List<WorkspaceChatCacheEntry>();
        if (!Directory.Exists(workspacePath))
            return chats;

        foreach (var chatPath in Directory.EnumerateDirectories(workspacePath))
        {
            if (!Guid.TryParse(Path.GetFileName(chatPath), out var chatId))
                continue;

            var chatName = await ReadNameOrDefaultAsync(Path.Join(chatPath, "name"), TB("Unnamed chat"));
            var chatThreadPath = Path.Join(chatPath, "thread.json");
            var lastEditTime = File.Exists(chatThreadPath) ? File.GetLastWriteTimeUtc(chatThreadPath) : DateTimeOffset.MinValue;
            chats.Add(new WorkspaceChatCacheEntry
            {
                WorkspaceId = workspaceId,
                ChatId = chatId,
                ChatPath = chatPath,
                ChatName = chatName,
                LastEditTime = lastEditTime,
                IsTemporary = false,
            });
        }

        return chats.OrderByDescending(x => x.LastEditTime).ToList();
    }

    private static async Task<List<WorkspaceChatCacheEntry>> ReadTemporaryChatsCoreAsync()
    {
        var chats = new List<WorkspaceChatCacheEntry>();
        Directory.CreateDirectory(TemporaryChatsRootDirectory);

        foreach (var tempChatPath in Directory.EnumerateDirectories(TemporaryChatsRootDirectory))
        {
            if (!Guid.TryParse(Path.GetFileName(tempChatPath), out var chatId))
                continue;

            var chatName = await ReadNameOrDefaultAsync(Path.Join(tempChatPath, "name"), TB("Unnamed chat"));
            var chatThreadPath = Path.Join(tempChatPath, "thread.json");
            var lastEditTime = File.Exists(chatThreadPath) ? File.GetLastWriteTimeUtc(chatThreadPath) : DateTimeOffset.MinValue;
            chats.Add(new WorkspaceChatCacheEntry
            {
                WorkspaceId = Guid.Empty,
                ChatId = chatId,
                ChatPath = tempChatPath,
                ChatName = chatName,
                LastEditTime = lastEditTime,
                IsTemporary = true,
            });
        }

        return chats.OrderByDescending(x => x.LastEditTime).ToList();
    }

    private static async Task EnsureTreeShellLoadedCoreAsync()
    {
        if (!WORKSPACE_TREE_CACHE_INVALIDATED && WORKSPACE_TREE_CACHE.IsShellLoaded)
            return;

        WORKSPACE_TREE_CACHE.Workspaces.Clear();
        WORKSPACE_TREE_CACHE.WorkspaceOrder.Clear();

        Directory.CreateDirectory(WorkspaceRootDirectory);
        foreach (var workspacePath in Directory.EnumerateDirectories(WorkspaceRootDirectory))
        {
            if (!Guid.TryParse(Path.GetFileName(workspacePath), out var workspaceId))
                continue;

            var workspaceName = await ReadNameOrDefaultAsync(Path.Join(workspacePath, "name"), TB("Unnamed workspace"));
            WORKSPACE_TREE_CACHE.Workspaces[workspaceId] = new WorkspaceCacheEntry
            {
                WorkspaceId = workspaceId,
                WorkspacePath = workspacePath,
                WorkspaceName = workspaceName,
                ChatsLoaded = false,
                Chats = [],
            };
            
            WORKSPACE_TREE_CACHE.WorkspaceOrder.Add(workspaceId);
        }

        WORKSPACE_TREE_CACHE.TemporaryChats = await ReadTemporaryChatsCoreAsync();
        WORKSPACE_TREE_CACHE.IsShellLoaded = true;
        WORKSPACE_TREE_CACHE_INVALIDATED = false;
    }

    private static void UpsertChatInCache(List<WorkspaceChatCacheEntry> chats, WorkspaceChatCacheEntry chat)
    {
        var existingIndex = chats.FindIndex(existing => existing.ChatId == chat.ChatId);
        if (existingIndex >= 0)
            chats[existingIndex] = chat;
        else
            chats.Add(chat);

        chats.Sort((a, b) => b.LastEditTime.CompareTo(a.LastEditTime));
    }

    private static void DeleteChatFromCache(List<WorkspaceChatCacheEntry> chats, Guid chatId)
    {
        var existingIndex = chats.FindIndex(existing => existing.ChatId == chatId);
        if (existingIndex >= 0)
            chats.RemoveAt(existingIndex);
    }

    private static async Task UpdateCacheAfterChatStored(Guid workspaceId, Guid chatId, string chatDirectory, string chatName, DateTimeOffset lastEditTime)
    {
        await WORKSPACE_TREE_CACHE_SEMAPHORE.WaitAsync();
        try
        {
            if (!WORKSPACE_TREE_CACHE.IsShellLoaded || WORKSPACE_TREE_CACHE_INVALIDATED)
                return;

            var chatCacheEntry = new WorkspaceChatCacheEntry
            {
                WorkspaceId = workspaceId,
                ChatId = chatId,
                ChatPath = chatDirectory,
                ChatName = string.IsNullOrWhiteSpace(chatName) ? TB("Unnamed chat") : chatName,
                LastEditTime = lastEditTime,
                IsTemporary = workspaceId == Guid.Empty,
            };

            if (workspaceId == Guid.Empty)
            {
                UpsertChatInCache(WORKSPACE_TREE_CACHE.TemporaryChats, chatCacheEntry);
                return;
            }

            if (WORKSPACE_TREE_CACHE.Workspaces.TryGetValue(workspaceId, out var workspace) && workspace.ChatsLoaded)
                UpsertChatInCache(workspace.Chats, chatCacheEntry);
        }
        finally
        {
            WORKSPACE_TREE_CACHE_SEMAPHORE.Release();
        }
    }

    private static async Task UpdateCacheAfterChatDeleted(Guid workspaceId, Guid chatId)
    {
        await WORKSPACE_TREE_CACHE_SEMAPHORE.WaitAsync();
        try
        {
            if (!WORKSPACE_TREE_CACHE.IsShellLoaded || WORKSPACE_TREE_CACHE_INVALIDATED)
                return;

            if (workspaceId == Guid.Empty)
            {
                DeleteChatFromCache(WORKSPACE_TREE_CACHE.TemporaryChats, chatId);
                return;
            }

            if (WORKSPACE_TREE_CACHE.Workspaces.TryGetValue(workspaceId, out var workspace) && workspace.ChatsLoaded)
                DeleteChatFromCache(workspace.Chats, chatId);
        }
        finally
        {
            WORKSPACE_TREE_CACHE_SEMAPHORE.Release();
        }
    }

    public static void InvalidateWorkspaceTreeCache()
    {
        WORKSPACE_TREE_CACHE_INVALIDATED = true;
    }

    public static async Task ForceReloadWorkspaceTreeAsync()
    {
        await WORKSPACE_TREE_CACHE_SEMAPHORE.WaitAsync();
        try
        {
            WORKSPACE_TREE_CACHE_INVALIDATED = false;
            WORKSPACE_TREE_CACHE.IsShellLoaded = false;
            await EnsureTreeShellLoadedCoreAsync();
        }
        finally
        {
            WORKSPACE_TREE_CACHE_SEMAPHORE.Release();
        }
    }

    public static async Task<WorkspaceTreeCacheSnapshot> GetOrLoadWorkspaceTreeShellAsync()
    {
        await WORKSPACE_TREE_CACHE_SEMAPHORE.WaitAsync();
        try
        {
            await EnsureTreeShellLoadedCoreAsync();
            var workspaces = WORKSPACE_TREE_CACHE.WorkspaceOrder
                .Where(workspaceId => WORKSPACE_TREE_CACHE.Workspaces.ContainsKey(workspaceId))
                .Select(workspaceId => ToPublicWorkspace(WORKSPACE_TREE_CACHE.Workspaces[workspaceId]))
                .ToList();
            var temporaryChats = WORKSPACE_TREE_CACHE.TemporaryChats.Select(ToPublicChat).ToList();
            return new WorkspaceTreeCacheSnapshot(workspaces, temporaryChats);
        }
        finally
        {
            WORKSPACE_TREE_CACHE_SEMAPHORE.Release();
        }
    }

    public static async Task<IReadOnlyList<WorkspaceTreeChat>> GetWorkspaceChatsAsync(Guid workspaceId, bool forceRefresh = false)
    {
        await WORKSPACE_TREE_CACHE_SEMAPHORE.WaitAsync();
        try
        {
            await EnsureTreeShellLoadedCoreAsync();
            if (!WORKSPACE_TREE_CACHE.Workspaces.TryGetValue(workspaceId, out var workspace))
                return [];

            if (forceRefresh || !workspace.ChatsLoaded)
            {
                workspace.Chats = await ReadWorkspaceChatsCoreAsync(workspaceId, workspace.WorkspacePath);
                workspace.ChatsLoaded = true;
            }

            return workspace.Chats.Select(ToPublicChat).ToList();
        }
        finally
        {
            WORKSPACE_TREE_CACHE_SEMAPHORE.Release();
        }
    }

    public static async Task TryPrefetchRemainingChatsAsync(Func<Guid, Task>? onWorkspaceUpdated = null, CancellationToken token = default)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            Guid? workspaceToPrefetch = null;

            await WORKSPACE_TREE_CACHE_SEMAPHORE.WaitAsync(token);
            try
            {
                await EnsureTreeShellLoadedCoreAsync();
                foreach (var workspaceId in WORKSPACE_TREE_CACHE.WorkspaceOrder)
                {
                    if (WORKSPACE_TREE_CACHE.Workspaces.TryGetValue(workspaceId, out var workspace) && !workspace.ChatsLoaded)
                    {
                        workspaceToPrefetch = workspaceId;
                        break;
                    }
                }
            }
            finally
            {
                WORKSPACE_TREE_CACHE_SEMAPHORE.Release();
            }

            if (workspaceToPrefetch is null)
                return;

            await GetWorkspaceChatsAsync(workspaceToPrefetch.Value);
            if (onWorkspaceUpdated is not null)
            {
                try
                {
                    await onWorkspaceUpdated(workspaceToPrefetch.Value);
                }
                catch (Exception ex)
                {
                    LOG.LogWarning(ex, "Failed to process callback after prefetching workspace '{WorkspaceId}'.", workspaceToPrefetch.Value);
                }
            }

            await Task.Delay(PREFETCH_DELAY_DURATION, token);
        }
    }

    public static async Task AddWorkspaceToCacheAsync(Guid workspaceId, string workspacePath, string workspaceName)
    {
        await WORKSPACE_TREE_CACHE_SEMAPHORE.WaitAsync();
        try
        {
            await EnsureTreeShellLoadedCoreAsync();
            if (WORKSPACE_TREE_CACHE.Workspaces.TryGetValue(workspaceId, out var workspace))
            {
                workspace.WorkspaceName = workspaceName;
                return;
            }

            WORKSPACE_TREE_CACHE.Workspaces[workspaceId] = new WorkspaceCacheEntry
            {
                WorkspaceId = workspaceId,
                WorkspacePath = workspacePath,
                WorkspaceName = workspaceName,
                Chats = [],
                ChatsLoaded = false,
            };
            WORKSPACE_TREE_CACHE.WorkspaceOrder.Add(workspaceId);
        }
        finally
        {
            WORKSPACE_TREE_CACHE_SEMAPHORE.Release();
        }
    }

    public static async Task UpdateWorkspaceNameInCacheAsync(Guid workspaceId, string workspaceName)
    {
        await WORKSPACE_TREE_CACHE_SEMAPHORE.WaitAsync();
        try
        {
            await EnsureTreeShellLoadedCoreAsync();
            if (WORKSPACE_TREE_CACHE.Workspaces.TryGetValue(workspaceId, out var workspace))
                workspace.WorkspaceName = workspaceName;
        }
        finally
        {
            WORKSPACE_TREE_CACHE_SEMAPHORE.Release();
        }
    }

    public static async Task RemoveWorkspaceFromCacheAsync(Guid workspaceId)
    {
        await WORKSPACE_TREE_CACHE_SEMAPHORE.WaitAsync();
        try
        {
            if (!WORKSPACE_TREE_CACHE.IsShellLoaded || WORKSPACE_TREE_CACHE_INVALIDATED)
                return;

            WORKSPACE_TREE_CACHE.Workspaces.Remove(workspaceId);
            WORKSPACE_TREE_CACHE.WorkspaceOrder.Remove(workspaceId);
        }
        finally
        {
            WORKSPACE_TREE_CACHE_SEMAPHORE.Release();
        }
    }
    
    public static bool IsChatExisting(LoadChat loadChat)
    {
        var chatPath = loadChat.WorkspaceId == Guid.Empty
            ? Path.Join(SettingsManager.DataDirectory, "tempChats", loadChat.ChatId.ToString())
            : Path.Join(SettingsManager.DataDirectory, "workspaces", loadChat.WorkspaceId.ToString(), loadChat.ChatId.ToString());
        
        return Directory.Exists(chatPath);
    }

    public static async Task StoreChatAsync(ChatThread chat)
    {
        var (acquired, semaphore) = await TryAcquireChatSemaphoreAsync(chat.WorkspaceId, chat.ChatId, nameof(StoreChatAsync));
        if (!acquired)
            return;

        try
        {
            var chatDirectory = chat.WorkspaceId == Guid.Empty
                ? Path.Join(SettingsManager.DataDirectory, "tempChats", chat.ChatId.ToString())
                : Path.Join(SettingsManager.DataDirectory, "workspaces", chat.WorkspaceId.ToString(), chat.ChatId.ToString());

            Directory.CreateDirectory(chatDirectory);

            var chatNamePath = Path.Join(chatDirectory, "name");
            await File.WriteAllTextAsync(chatNamePath, chat.Name);

            var chatPath = Path.Join(chatDirectory, "thread.json");
            await File.WriteAllTextAsync(chatPath, JsonSerializer.Serialize(chat, JSON_OPTIONS), Encoding.UTF8);

            var lastEditTime = File.GetLastWriteTimeUtc(chatPath);
            await UpdateCacheAfterChatStored(chat.WorkspaceId, chat.ChatId, chatDirectory, chat.Name, lastEditTime);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public static async Task<ChatThread?> LoadChatAsync(LoadChat loadChat)
    {
        var (acquired, semaphore) = await TryAcquireChatSemaphoreAsync(loadChat.WorkspaceId, loadChat.ChatId, nameof(LoadChatAsync));
        if (!acquired)
            return null;

        try
        {
            var chatPath = loadChat.WorkspaceId == Guid.Empty
                ? Path.Join(SettingsManager.DataDirectory, "tempChats", loadChat.ChatId.ToString())
                : Path.Join(SettingsManager.DataDirectory, "workspaces", loadChat.WorkspaceId.ToString(), loadChat.ChatId.ToString());

            if (!Directory.Exists(chatPath))
                return null;

            var chatData = await File.ReadAllTextAsync(Path.Join(chatPath, "thread.json"), Encoding.UTF8);
            return JsonSerializer.Deserialize<ChatThread>(chatData, JSON_OPTIONS);
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            semaphore.Release();
        }
    }
    
    public static async Task<string> LoadWorkspaceNameAsync(Guid workspaceId)
    {
        if (workspaceId == Guid.Empty)
            return string.Empty;

        await WORKSPACE_TREE_CACHE_SEMAPHORE.WaitAsync();
        try
        {
            await EnsureTreeShellLoadedCoreAsync();
            if (WORKSPACE_TREE_CACHE.Workspaces.TryGetValue(workspaceId, out var cachedWorkspace) && !string.IsNullOrWhiteSpace(cachedWorkspace.WorkspaceName))
                return cachedWorkspace.WorkspaceName;
        }
        finally
        {
            WORKSPACE_TREE_CACHE_SEMAPHORE.Release();
        }

        var workspacePath = Path.Join(WorkspaceRootDirectory, workspaceId.ToString());
        var workspaceNamePath = Path.Join(workspacePath, "name");
        string workspaceName;
        
        try
        {
            if (!File.Exists(workspaceNamePath))
            {
                workspaceName = TB("Unnamed workspace");
                Directory.CreateDirectory(workspacePath);
                await File.WriteAllTextAsync(workspaceNamePath, workspaceName, Encoding.UTF8);
            }
            else
            {
                workspaceName = await File.ReadAllTextAsync(workspaceNamePath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(workspaceName))
                {
                    workspaceName = TB("Unnamed workspace");
                    await File.WriteAllTextAsync(workspaceNamePath, workspaceName, Encoding.UTF8);
                }
            }
        }
        catch
        {
            workspaceName = TB("Unnamed workspace");
        }

        await UpdateWorkspaceNameInCacheAsync(workspaceId, workspaceName);
        return workspaceName;
    }
    
    public static async Task DeleteChatAsync(IDialogService dialogService, Guid workspaceId, Guid chatId, bool askForConfirmation = true)
    {
        var chat = await LoadChatAsync(new(workspaceId, chatId));
        if (chat is null)
            return;

        if (askForConfirmation)
        {
            var workspaceName = await LoadWorkspaceNameAsync(chat.WorkspaceId);
            var dialogParameters = new DialogParameters<ConfirmDialog>
            {
                {
                    x => x.Message, (chat.WorkspaceId == Guid.Empty) switch
                    {
                        true => TB($"Are you sure you want to delete the temporary chat '{chat.Name}'?"),
                        false => TB($"Are you sure you want to delete the chat '{chat.Name}' in the workspace '{workspaceName}'?"),
                    }
                },
            };

            var dialogReference = await dialogService.ShowAsync<ConfirmDialog>(TB("Delete Chat"), dialogParameters, Dialogs.DialogOptions.FULLSCREEN);
            var dialogResult = await dialogReference.Result;
            if (dialogResult is null || dialogResult.Canceled)
                return;
        }

        var chatDirectory = chat.WorkspaceId == Guid.Empty
            ? Path.Join(SettingsManager.DataDirectory, "tempChats", chat.ChatId.ToString())
            : Path.Join(SettingsManager.DataDirectory, "workspaces", chat.WorkspaceId.ToString(), chat.ChatId.ToString());

        var (acquired, semaphore) = await TryAcquireChatSemaphoreAsync(workspaceId, chatId, nameof(DeleteChatAsync));
        if (!acquired)
            return;

        try
        {
            if (Directory.Exists(chatDirectory))
                Directory.Delete(chatDirectory, true);

            await UpdateCacheAfterChatDeleted(workspaceId, chatId);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static async Task EnsureWorkspace(Guid workspaceId, string workspaceName)
    {
        var workspacePath = Path.Join(WorkspaceRootDirectory, workspaceId.ToString());
        var workspaceNamePath = Path.Join(workspacePath, "name");
        
        if (!Path.Exists(workspacePath))
            Directory.CreateDirectory(workspacePath);
        
        try
        {
            if (!File.Exists(workspaceNamePath))
            {
                await File.WriteAllTextAsync(workspaceNamePath, workspaceName, Encoding.UTF8);
            }
            else
            {
                var existing = await File.ReadAllTextAsync(workspaceNamePath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(existing))
                    await File.WriteAllTextAsync(workspaceNamePath, workspaceName, Encoding.UTF8);
            }
        }
        catch
        {
            // Ignore IO issues to avoid interrupting background initialization.
        }

        await AddWorkspaceToCacheAsync(workspaceId, workspacePath, workspaceName);
    }

    public static async Task EnsureBiasWorkspace() => await EnsureWorkspace(KnownWorkspaces.BIAS_WORKSPACE_ID, "Bias of the Day");

    public static async Task EnsureERIServerWorkspace() => await EnsureWorkspace(KnownWorkspaces.ERI_SERVER_WORKSPACE_ID, "ERI Servers");
}
