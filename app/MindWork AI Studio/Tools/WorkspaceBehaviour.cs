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
    public readonly record struct TryCreateWorkspaceResult(bool Success, WorkspaceTreeWorkspace Workspace);

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

    private static readonly string WORKSPACE_ROOT_DIRECTORY = Path.Join(SettingsManager.DataDirectory, "workspaces");

    private static readonly string TEMPORARY_CHATS_ROOT_DIRECTORY = Path.Join(SettingsManager.DataDirectory, "tempChats");

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
        Directory.CreateDirectory(TEMPORARY_CHATS_ROOT_DIRECTORY);

        foreach (var tempChatPath in Directory.EnumerateDirectories(TEMPORARY_CHATS_ROOT_DIRECTORY))
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

        Directory.CreateDirectory(WORKSPACE_ROOT_DIRECTORY);
        foreach (var workspacePath in Directory.EnumerateDirectories(WORKSPACE_ROOT_DIRECTORY))
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

    private static IReadOnlyList<string> ParseSearchTerms(string searchText) => searchText
        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(term => !string.IsNullOrWhiteSpace(term))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static IReadOnlyList<string> GetMissingTerms(string text, IReadOnlyList<string> terms) => terms
        .Where(term => text.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0)
        .ToList();

    private static bool ChatThreadContainsTerms(ChatThread thread, IReadOnlyList<string> terms)
    {
        var matchedTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var block in thread.Blocks)
        {
            if (block.HideFromUser || block.Content is not ContentText textContent || string.IsNullOrWhiteSpace(textContent.Text))
                continue;

            foreach (var term in terms)
                if (textContent.Text.Contains(term, StringComparison.OrdinalIgnoreCase))
                    matchedTerms.Add(term);

            if (matchedTerms.Count == terms.Count)
                return true;
        }

        return false;
    }

    private static bool WorkspaceNameExistsCore(string workspaceName, Guid excludedWorkspaceId = default)
    {
        return WORKSPACE_TREE_CACHE.Workspaces.Values.Any(workspace =>
            workspace.WorkspaceId != excludedWorkspaceId &&
            string.Equals(workspace.WorkspaceName.Trim(), workspaceName, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<bool> ThreadContainsTermsAsync(WorkspaceTreeChat chat, IReadOnlyList<string> terms, CancellationToken token)
    {
        var (acquired, semaphore) = await TryAcquireChatSemaphoreAsync(chat.WorkspaceId, chat.ChatId, nameof(ThreadContainsTermsAsync));
        if (!acquired)
            return false;

        try
        {
            var threadPath = Path.Join(chat.ChatPath, "thread.json");
            if (!File.Exists(threadPath))
                return false;

            var chatData = await File.ReadAllTextAsync(threadPath, Encoding.UTF8, token);
            token.ThrowIfCancellationRequested();
            var thread = JsonSerializer.Deserialize<ChatThread>(chatData, JSON_OPTIONS);
            return thread is not null && ChatThreadContainsTerms(thread, terms);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LOG.LogWarning(ex, "Failed to search chat thread for workspace '{WorkspaceId}', chat '{ChatId}'.", chat.WorkspaceId, chat.ChatId);
            return false;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static async Task<List<WorkspaceSearchResult>> SearchChatsAsync(IReadOnlyList<WorkspaceTreeChat> chats, IReadOnlyList<string> terms, bool includeThreadContents, CancellationToken token)
    {
        var results = new List<WorkspaceSearchResult>();
        foreach (var chat in chats)
        {
            token.ThrowIfCancellationRequested();

            var missingTerms = GetMissingTerms(chat.Name, terms);
            if (missingTerms.Count == 0)
            {
                results.Add(new(chat, NameMatched: true, ThreadMatched: false));
                continue;
            }

            if (!includeThreadContents)
                continue;

            var threadMatched = await ThreadContainsTermsAsync(chat, missingTerms, token);
            if (threadMatched)
                results.Add(new(chat, NameMatched: false, ThreadMatched: true));
        }

        return results;
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

    public static async Task<WorkspaceSearchSnapshot> SearchWorkspaceChatsAsync(string searchText, bool includeThreadContents, CancellationToken token = default)
    {
        var terms = ParseSearchTerms(searchText);
        if (terms.Count == 0)
            return new([], []);

        List<WorkspaceTreeWorkspace> workspaces;
        List<WorkspaceTreeChat> temporaryChats;

        await WORKSPACE_TREE_CACHE_SEMAPHORE.WaitAsync(token);
        try
        {
            await EnsureTreeShellLoadedCoreAsync();
            workspaces = [];
            foreach (var workspaceId in WORKSPACE_TREE_CACHE.WorkspaceOrder)
            {
                token.ThrowIfCancellationRequested();
                if (!WORKSPACE_TREE_CACHE.Workspaces.TryGetValue(workspaceId, out var workspace))
                    continue;

                if (!workspace.ChatsLoaded)
                {
                    workspace.Chats = await ReadWorkspaceChatsCoreAsync(workspaceId, workspace.WorkspacePath);
                    workspace.ChatsLoaded = true;
                }

                workspaces.Add(ToPublicWorkspace(workspace));
            }

            temporaryChats = WORKSPACE_TREE_CACHE.TemporaryChats.Select(ToPublicChat).ToList();
        }
        finally
        {
            WORKSPACE_TREE_CACHE_SEMAPHORE.Release();
        }

        var matchingWorkspaces = new List<WorkspaceSearchWorkspace>();
        foreach (var workspace in workspaces)
        {
            token.ThrowIfCancellationRequested();
            var matchingChats = await SearchChatsAsync(workspace.Chats, terms, includeThreadContents, token);
            if (matchingChats.Count > 0)
                matchingWorkspaces.Add(new(workspace.WorkspaceId, workspace.WorkspacePath, workspace.Name, matchingChats));
        }

        var matchingTemporaryChats = await SearchChatsAsync(temporaryChats, terms, includeThreadContents, token);
        return new(matchingWorkspaces, matchingTemporaryChats);
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

    public static string NormalizeWorkspaceName(string workspaceName) => workspaceName.Trim();

    public static async Task<bool> IsWorkspaceNameExistingAsync(string workspaceName, Guid excludedWorkspaceId = default)
    {
        var normalizedWorkspaceName = NormalizeWorkspaceName(workspaceName);
        if (string.IsNullOrWhiteSpace(normalizedWorkspaceName))
            return false;

        await WORKSPACE_TREE_CACHE_SEMAPHORE.WaitAsync();
        try
        {
            await EnsureTreeShellLoadedCoreAsync();
            return WorkspaceNameExistsCore(normalizedWorkspaceName, excludedWorkspaceId);
        }
        finally
        {
            WORKSPACE_TREE_CACHE_SEMAPHORE.Release();
        }
    }

    public static async Task<Guid> ResolveOrCreateWorkspaceIdByNameAsync(string workspaceName)
    {
        var normalizedWorkspaceName = NormalizeWorkspaceName(workspaceName);
        if (string.IsNullOrWhiteSpace(normalizedWorkspaceName))
            return Guid.Empty;

        await WORKSPACE_TREE_CACHE_SEMAPHORE.WaitAsync();
        try
        {
            await EnsureTreeShellLoadedCoreAsync();

            var existingWorkspace = WORKSPACE_TREE_CACHE.Workspaces.Values.FirstOrDefault(workspace =>
                string.Equals(workspace.WorkspaceName.Trim(), normalizedWorkspaceName, StringComparison.OrdinalIgnoreCase));
            if (existingWorkspace is not null)
                return existingWorkspace.WorkspaceId;
        }
        finally
        {
            WORKSPACE_TREE_CACHE_SEMAPHORE.Release();
        }

        var result = await TryCreateWorkspaceAsync(normalizedWorkspaceName);
        if (result.Success)
            return result.Workspace.WorkspaceId;

        await WORKSPACE_TREE_CACHE_SEMAPHORE.WaitAsync();
        try
        {
            await EnsureTreeShellLoadedCoreAsync();

            var existingWorkspace = WORKSPACE_TREE_CACHE.Workspaces.Values.FirstOrDefault(workspace =>
                string.Equals(workspace.WorkspaceName.Trim(), normalizedWorkspaceName, StringComparison.OrdinalIgnoreCase));
            return existingWorkspace?.WorkspaceId ?? Guid.Empty;
        }
        finally
        {
            WORKSPACE_TREE_CACHE_SEMAPHORE.Release();
        }
    }

    public static async Task<TryCreateWorkspaceResult> TryCreateWorkspaceAsync(string workspaceName)
    {
        var normalizedWorkspaceName = NormalizeWorkspaceName(workspaceName);
        if (string.IsNullOrWhiteSpace(normalizedWorkspaceName))
            return new(false, default);

        await WORKSPACE_TREE_CACHE_SEMAPHORE.WaitAsync();
        try
        {
            await EnsureTreeShellLoadedCoreAsync();
            if (WorkspaceNameExistsCore(normalizedWorkspaceName))
                return new(false, default);

            var workspaceId = Guid.NewGuid();
            var workspacePath = Path.Join(WORKSPACE_ROOT_DIRECTORY, workspaceId.ToString());
            Directory.CreateDirectory(workspacePath);

            var workspaceNamePath = Path.Join(workspacePath, "name");
            await File.WriteAllTextAsync(workspaceNamePath, normalizedWorkspaceName, Encoding.UTF8);

            var workspace = new WorkspaceCacheEntry
            {
                WorkspaceId = workspaceId,
                WorkspacePath = workspacePath,
                WorkspaceName = normalizedWorkspaceName,
                Chats = [],
                ChatsLoaded = false,
            };
            WORKSPACE_TREE_CACHE.Workspaces[workspaceId] = workspace;
            WORKSPACE_TREE_CACHE.WorkspaceOrder.Add(workspaceId);

            return new(true, ToPublicWorkspace(workspace));
        }
        finally
        {
            WORKSPACE_TREE_CACHE_SEMAPHORE.Release();
        }
    }

    public static async Task<bool> RenameWorkspaceAsync(Guid workspaceId, string workspaceName)
    {
        var normalizedWorkspaceName = NormalizeWorkspaceName(workspaceName);
        if (string.IsNullOrWhiteSpace(normalizedWorkspaceName))
            return false;

        await WORKSPACE_TREE_CACHE_SEMAPHORE.WaitAsync();
        try
        {
            await EnsureTreeShellLoadedCoreAsync();
            if (!WORKSPACE_TREE_CACHE.Workspaces.TryGetValue(workspaceId, out var workspace))
                return false;

            var workspaceNamePath = Path.Join(workspace.WorkspacePath, "name");
            if (string.Equals(workspace.WorkspaceName.Trim(), normalizedWorkspaceName, StringComparison.OrdinalIgnoreCase))
            {
                await File.WriteAllTextAsync(workspaceNamePath, normalizedWorkspaceName, Encoding.UTF8);
                workspace.WorkspaceName = normalizedWorkspaceName;
                return true;
            }

            if (WorkspaceNameExistsCore(normalizedWorkspaceName, workspaceId))
                return false;

            await File.WriteAllTextAsync(workspaceNamePath, normalizedWorkspaceName, Encoding.UTF8);
            workspace.WorkspaceName = normalizedWorkspaceName;

            return true;
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

            await FinalizeStagedTranscriptsAsync(chat, chatDirectory);
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

    /// <summary>Creates a transcript atomically inside an already persisted chat.</summary>
    /// <param name="chat">Persisted chat that owns the transcript counter.</param>
    /// <param name="originalPath">Original media path.</param>
    /// <param name="transcript">Provider transcript.</param>
    /// <returns>The chat-owned managed attachment.</returns>
    public static async Task<ManagedTranscriptAttachment> CreateManagedTranscriptAsync(ChatThread chat, string originalPath, string transcript)
    {
        var (acquired, semaphore) = await TryAcquireChatSemaphoreAsync(chat.WorkspaceId, chat.ChatId, nameof(CreateManagedTranscriptAsync));
        if (!acquired)
            throw new IOException("The chat transcript directory is busy.");

        try
        {
            var chatDirectory = GetChatDirectory(chat.WorkspaceId, chat.ChatId);
            if (!Directory.Exists(chatDirectory))
                throw new DirectoryNotFoundException($"The owning chat directory does not exist: '{chatDirectory}'.");
            
            var transcriptDirectory = Path.Combine(chatDirectory, "attachments", "transcripts");
            Directory.CreateDirectory(transcriptDirectory);
            ReconcileTranscriptCounter(chat, transcriptDirectory);
            
            var targetPath = NextTranscriptPath(chat, transcriptDirectory, Path.GetFileName(originalPath));
            return await ManagedTranscriptAttachment.CreateAtomicAsync(targetPath, Path.GetFileName(originalPath), transcript);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public static async Task MoveChatAsync(ChatThread chat, Guid targetWorkspaceId)
    {
        if (chat.WorkspaceId == targetWorkspaceId)
            return;

        var sourceWorkspaceId = chat.WorkspaceId;
        var sourceDirectory = GetChatDirectory(sourceWorkspaceId, chat.ChatId);
        var targetDirectory = GetChatDirectory(targetWorkspaceId, chat.ChatId);
        var sourceSemaphore = GetChatSemaphore(sourceWorkspaceId, chat.ChatId);
        var targetSemaphore = GetChatSemaphore(targetWorkspaceId, chat.ChatId);
        var orderedSemaphores = string.CompareOrdinal(sourceWorkspaceId.ToString("N"), targetWorkspaceId.ToString("N")) <= 0
            ? new[] { sourceSemaphore, targetSemaphore }
            : new[] { targetSemaphore, sourceSemaphore };
        
        await orderedSemaphores[0].WaitAsync();
        await orderedSemaphores[1].WaitAsync();
        
        var moved = false;
        try
        {
            if (!Directory.Exists(sourceDirectory))
                throw new DirectoryNotFoundException($"The source chat directory does not exist: '{sourceDirectory}'.");
            
            Directory.CreateDirectory(Path.GetDirectoryName(targetDirectory)!);
            if (Directory.Exists(targetDirectory))
                throw new IOException($"The target chat directory already exists: '{targetDirectory}'.");

            Directory.Move(sourceDirectory, targetDirectory);
            moved = true;
            
            UpdateAttachmentPathsAfterMove(chat, sourceDirectory, targetDirectory);
            chat.WorkspaceId = targetWorkspaceId;
            
            await FinalizeStagedTranscriptsAsync(chat, targetDirectory);
            await StoreMovedChatFilesAsync(chat, targetDirectory);
        }
        catch
        {
            if (moved)
            {
                try
                {
                    UpdateAttachmentPathsAfterMove(chat, targetDirectory, sourceDirectory);
                    chat.WorkspaceId = sourceWorkspaceId;
                    
                    if (Directory.Exists(targetDirectory) && !Directory.Exists(sourceDirectory))
                        Directory.Move(targetDirectory, sourceDirectory);
                    
                    if (Directory.Exists(sourceDirectory))
                        await StoreMovedChatFilesAsync(chat, sourceDirectory);
                }
                catch (Exception rollbackError)
                {
                    LOG.LogError(rollbackError, "Could not roll back moving chat '{ChatId}' to workspace '{WorkspaceId}'.", chat.ChatId, targetWorkspaceId);
                }
            }
            throw;
        }
        finally
        {
            orderedSemaphores[1].Release();
            orderedSemaphores[0].Release();
            InvalidateWorkspaceTreeCache();
        }
    }

    /// <summary>Atomically stores the name and thread after a directory move.</summary>
    private static async Task StoreMovedChatFilesAsync(ChatThread chat, string chatDirectory)
    {
        await File.WriteAllTextAsync(Path.Join(chatDirectory, "name"), chat.Name);
        var chatPath = Path.Join(chatDirectory, "thread.json");
        var temporaryPath = Path.Join(chatDirectory, $".thread-{Guid.NewGuid():N}.tmp");
        
        try
        {
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(chat, JSON_OPTIONS), Encoding.UTF8);
            File.Move(temporaryPath, chatPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    /// <summary>Rewrites absolute attachment paths after moving the complete chat directory.</summary>
    private static void UpdateAttachmentPathsAfterMove(ChatThread chat, string sourceDirectory, string targetDirectory)
    {
        var sourcePrefix = sourceDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? sourceDirectory
            : sourceDirectory + Path.DirectorySeparatorChar;

        var pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        foreach (var content in chat.Blocks.Select(block => block.Content).OfType<ContentText>())
        {
            for (var index = 0; index < content.FileAttachments.Count; index++)
            {
                var attachment = content.FileAttachments[index];
                if (!Path.GetFullPath(attachment.FilePath).StartsWith(sourcePrefix, pathComparison))
                    continue;

                var relativePath = Path.GetRelativePath(sourceDirectory, attachment.FilePath);
                var movedPath = Path.Combine(targetDirectory, relativePath);

                content.FileAttachments[index] = attachment switch
                {
                    ManagedTranscriptAttachment managed => managed with { FilePath = movedPath },
                    FileAttachmentImage image => image with { FilePath = movedPath },
                    _ => attachment with { FilePath = movedPath },
                };
            }
        }
    }

    private static async Task FinalizeStagedTranscriptsAsync(ChatThread chat, string chatDirectory)
    {
        var transcriptDirectory = Path.Combine(chatDirectory, "attachments", "transcripts");
        ReconcileTranscriptCounter(chat, transcriptDirectory);
        foreach (var content in chat.Blocks.Select(block => block.Content).OfType<ContentText>())
        {
            for (var index = 0; index < content.FileAttachments.Count; index++)
            {
                if (content.FileAttachments[index] is not ManagedTranscriptAttachment { IsStaged: true } staged
                    || !File.Exists(staged.FilePath))
                    continue;

                Directory.CreateDirectory(transcriptDirectory);
                var targetPath = NextTranscriptPath(chat, transcriptDirectory, staged.OriginalFileName);

                File.Move(staged.FilePath, targetPath);
                var sourceDirectory = Path.GetDirectoryName(staged.FilePath);
                if (sourceDirectory is not null && Directory.Exists(sourceDirectory) && !Directory.EnumerateFileSystemEntries(sourceDirectory).Any())
                    Directory.Delete(sourceDirectory);

                content.FileAttachments[index] = new ManagedTranscriptAttachment(
                    Path.GetFileName(targetPath),
                    targetPath,
                    new FileInfo(targetPath).Length,
                    staged.OriginalFileName,
                    false);
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>Raises the persisted counter to the highest transcript suffix found chat-wide.</summary>
    private static void ReconcileTranscriptCounter(ChatThread chat, string transcriptDirectory)
    {
        if (!Directory.Exists(transcriptDirectory))
            return;
        
        ulong highest = 0;
        foreach (var path in Directory.EnumerateFiles(transcriptDirectory, "*-transcript-*.md", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var marker = name.LastIndexOf("-transcript-", StringComparison.Ordinal);
            
            if (marker >= 0 && ulong.TryParse(name[(marker + "-transcript-".Length)..], out var number))
                highest = Math.Max(highest, number);
        }
        
        chat.LastMediaTranscriptNumber = Math.Max(chat.LastMediaTranscriptNumber, highest);
    }

    /// <summary>Allocates the next globally monotonic transcript path for one chat.</summary>
    private static string NextTranscriptPath(ChatThread chat, string transcriptDirectory, string originalFileName)
    {
        string targetPath;
        do
        {
            chat.LastMediaTranscriptNumber++;
            var stem = ManagedTranscriptAttachment.NormalizeOriginalStem(originalFileName);
            targetPath = Path.Combine(transcriptDirectory, $"{stem}-transcript-{chat.LastMediaTranscriptNumber:D4}.md");
        } while (File.Exists(targetPath));
        
        return targetPath;
    }

    /// <summary>Returns the canonical storage directory for a chat identity.</summary>
    private static string GetChatDirectory(Guid workspaceId, Guid chatId) => workspaceId == Guid.Empty
        ? Path.Join(SettingsManager.DataDirectory, "tempChats", chatId.ToString())
        : Path.Join(SettingsManager.DataDirectory, "workspaces", workspaceId.ToString(), chatId.ToString());

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

            // Not in cache — read from disk and update cache in the same semaphore scope
            // to avoid a second semaphore acquisition via UpdateWorkspaceNameInCacheAsync:
            var workspacePath = Path.Join(WORKSPACE_ROOT_DIRECTORY, workspaceId.ToString());
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

            // Update the cache directly (we already hold the semaphore):
            if (WORKSPACE_TREE_CACHE.Workspaces.TryGetValue(workspaceId, out var workspace))
                workspace.WorkspaceName = workspaceName;

            return workspaceName;
        }
        finally
        {
            WORKSPACE_TREE_CACHE_SEMAPHORE.Release();
        }
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
        var workspacePath = Path.Join(WORKSPACE_ROOT_DIRECTORY, workspaceId.ToString());
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