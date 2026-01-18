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
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(WorkspaceBehaviour).Namespace, nameof(WorkspaceBehaviour));

    /// <summary>
    /// Semaphores for synchronizing chat storage operations per chat.
    /// This prevents race conditions when multiple threads try to write
    /// the same chat file simultaneously.
    /// </summary>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> CHAT_STORAGE_SEMAPHORES = new();

    private static SemaphoreSlim GetChatSemaphore(Guid workspaceId, Guid chatId)
    {
        var key = $"{workspaceId}_{chatId}";
        return CHAT_STORAGE_SEMAPHORES.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
    }

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
    
    public static bool IsChatExisting(LoadChat loadChat)
    {
        var chatPath = loadChat.WorkspaceId == Guid.Empty
            ? Path.Join(SettingsManager.DataDirectory, "tempChats", loadChat.ChatId.ToString())
            : Path.Join(SettingsManager.DataDirectory, "workspaces", loadChat.WorkspaceId.ToString(), loadChat.ChatId.ToString());
        
        return Directory.Exists(chatPath);
    }

    public static async Task StoreChat(ChatThread chat)
    {
        // Acquire the semaphore for this specific chat to prevent concurrent writes to the same file:
        var semaphore = GetChatSemaphore(chat.WorkspaceId, chat.ChatId);
        await semaphore.WaitAsync();
        
        try
        {
            string chatDirectory;
            if (chat.WorkspaceId == Guid.Empty)
                chatDirectory = Path.Join(SettingsManager.DataDirectory, "tempChats", chat.ChatId.ToString());
            else
                chatDirectory = Path.Join(SettingsManager.DataDirectory, "workspaces", chat.WorkspaceId.ToString(), chat.ChatId.ToString());

            // Ensure the directory exists:
            Directory.CreateDirectory(chatDirectory);

            // Save the chat name:
            var chatNamePath = Path.Join(chatDirectory, "name");
            await File.WriteAllTextAsync(chatNamePath, chat.Name);

            // Save the thread as thread.json:
            var chatPath = Path.Join(chatDirectory, "thread.json");
            await File.WriteAllTextAsync(chatPath, JsonSerializer.Serialize(chat, JSON_OPTIONS), Encoding.UTF8);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public static async Task<ChatThread?> LoadChat(LoadChat loadChat)
    {
        // Acquire the semaphore for this specific chat to prevent concurrent read/writes to the same file:
        var semaphore = GetChatSemaphore(loadChat.WorkspaceId, loadChat.ChatId);
        await semaphore.WaitAsync();
        
        try
        {
            var chatPath = loadChat.WorkspaceId == Guid.Empty
                ? Path.Join(SettingsManager.DataDirectory, "tempChats", loadChat.ChatId.ToString())
                : Path.Join(SettingsManager.DataDirectory, "workspaces", loadChat.WorkspaceId.ToString(), loadChat.ChatId.ToString());

            if(!Directory.Exists(chatPath))
                return null;
            
            var chatData = await File.ReadAllTextAsync(Path.Join(chatPath, "thread.json"), Encoding.UTF8);
            var chat = JsonSerializer.Deserialize<ChatThread>(chatData, JSON_OPTIONS);
            return chat;
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
    
    public static async Task<string> LoadWorkspaceName(Guid workspaceId)
    {
        if(workspaceId == Guid.Empty)
            return string.Empty;
        
        var workspacePath = Path.Join(SettingsManager.DataDirectory, "workspaces", workspaceId.ToString());
        var workspaceNamePath = Path.Join(workspacePath, "name");
        
        try
        {
            // If the name file does not exist or is empty, self-heal with a default name.
            if (!File.Exists(workspaceNamePath))
            {
                var defaultName = TB("Unnamed workspace");
                Directory.CreateDirectory(workspacePath);
                await File.WriteAllTextAsync(workspaceNamePath, defaultName, Encoding.UTF8);
                return defaultName;
            }
            
            var name = await File.ReadAllTextAsync(workspaceNamePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(name))
            {
                var defaultName = TB("Unnamed workspace");
                await File.WriteAllTextAsync(workspaceNamePath, defaultName, Encoding.UTF8);
                return defaultName;
            }
            
            return name;
        }
        catch
        {
            // On any error, return a localized default without throwing.
            return TB("Unnamed workspace");
        }
    }
    
    public static async Task DeleteChat(IDialogService dialogService, Guid workspaceId, Guid chatId, bool askForConfirmation = true)
    {
        var chat = await LoadChat(new(workspaceId, chatId));
        if (chat is null)
            return;

        if (askForConfirmation)
        {
            var workspaceName = await LoadWorkspaceName(chat.WorkspaceId);
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

        string chatDirectory;
        if (chat.WorkspaceId == Guid.Empty)
            chatDirectory = Path.Join(SettingsManager.DataDirectory, "tempChats", chat.ChatId.ToString());
        else
            chatDirectory = Path.Join(SettingsManager.DataDirectory, "workspaces", chat.WorkspaceId.ToString(), chat.ChatId.ToString());

        // Acquire the semaphore to prevent deleting while another thread is writing:
        var semaphore = GetChatSemaphore(workspaceId, chatId);
        await semaphore.WaitAsync();
        
        try
        {
            Directory.Delete(chatDirectory, true);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static async Task EnsureWorkspace(Guid workspaceId, string workspaceName)
    {
        var workspacePath = Path.Join(SettingsManager.DataDirectory, "workspaces", workspaceId.ToString());
        var workspaceNamePath = Path.Join(workspacePath, "name");
        
        if(!Path.Exists(workspacePath))
            Directory.CreateDirectory(workspacePath);
        
        try
        {
            // When the name file is missing or empty, write it (self-heal).
            // Otherwise, keep the existing name:
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
    }
    
    public static async Task EnsureBiasWorkspace() => await EnsureWorkspace(KnownWorkspaces.BIAS_WORKSPACE_ID, "Bias of the Day");
    
    public static async Task EnsureERIServerWorkspace() => await EnsureWorkspace(KnownWorkspaces.ERI_SERVER_WORKSPACE_ID, "ERI Servers");
}