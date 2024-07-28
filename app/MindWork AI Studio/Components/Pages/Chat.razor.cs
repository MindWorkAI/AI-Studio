using AIStudio.Chat;
using AIStudio.Components.Blocks;
using AIStudio.Components.CommonDialogs;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using DialogOptions = AIStudio.Components.CommonDialogs.DialogOptions;

namespace AIStudio.Components.Pages;

/// <summary>
/// The chat page.
/// </summary>
public partial class Chat : MSGComponentBase, IAsyncDisposable
{
    [Inject]
    private SettingsManager SettingsManager { get; set; } = null!;
    
    [Inject]
    public IJSRuntime JsRuntime { get; init; } = null!;

    [Inject]
    public Random RNG { get; set; } = null!;
    
    [Inject]
    public IDialogService DialogService { get; set; } = null!;

    private const Placement TOOLBAR_TOOLTIP_PLACEMENT = Placement.Bottom;
    private static readonly Dictionary<string, object?> USER_INPUT_ATTRIBUTES = new();
    
    private AIStudio.Settings.Provider providerSettings;
    private ChatThread? chatThread;
    private bool hasUnsavedChanges;
    private bool isStreaming;
    private string userInput = string.Empty;
    private string currentWorkspaceName = string.Empty;
    private Guid currentWorkspaceId = Guid.Empty;
    private bool workspacesVisible;
    private Workspaces? workspaces;
    
    // Unfortunately, we need the input field reference to clear it after sending a message.
    // This is necessary because we have to handle the key events ourselves. Otherwise,
    // the clearing would be done automatically.
    private MudTextField<string> inputField = null!;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.HAS_CHAT_UNSAVED_CHANGES, Event.RESET_CHAT_STATE ]);
        
        // Configure the spellchecking for the user input:
        this.SettingsManager.InjectSpellchecking(USER_INPUT_ATTRIBUTES);
        
        await base.OnInitializedAsync();
    }

    #endregion

    private bool IsProviderSelected => this.providerSettings.UsedProvider != Providers.NONE;
    
    private string ProviderPlaceholder => this.IsProviderSelected ? "Type your input here..." : "Select a provider first";

    private string InputLabel => this.IsProviderSelected ? $"Your Prompt (use selected instance '{this.providerSettings.InstanceName}', provider '{this.providerSettings.UsedProvider.ToName()}')" : "Select a provider first";
    
    private bool CanThreadBeSaved => this.chatThread is not null && this.chatThread.Blocks.Count > 0;

    private string TooltipAddChatToWorkspace => $"Start new chat in workspace \"{this.currentWorkspaceName}\"";
    
    private async Task SendMessage()
    {
        if (!this.IsProviderSelected)
            return;
        
        // Create a new chat thread if necessary:
        var threadName = this.ExtractThreadName(this.userInput);

        if (this.chatThread is null)
        {
            this.chatThread = new()
            {
                WorkspaceId = this.currentWorkspaceId,
                ChatId = Guid.NewGuid(),
                Name = threadName,
                Seed = this.RNG.Next(),
                SystemPrompt = "You are a helpful assistant!",
                Blocks = [],
            };
        }
        else
        {
            // Set the thread name if it is empty:
            if (string.IsNullOrWhiteSpace(this.chatThread.Name))
                this.chatThread.Name = threadName;
        }
        
        //
        // Add the user message to the thread:
        //
        var time = DateTimeOffset.Now;
        this.chatThread?.Blocks.Add(new ContentBlock
        {
            Time = time,
            ContentType = ContentType.TEXT,
            Role = ChatRole.USER,
            Content = new ContentText
            {
                Text = this.userInput,
            },
        });

        // Save the chat:
        if (this.SettingsManager.ConfigurationData.WorkspaceStorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY)
        {
            await this.SaveThread();
            this.hasUnsavedChanges = false;
            this.StateHasChanged();
        }

        //
        // Add the AI response to the thread:
        //
        var aiText = new ContentText
        {
            // We have to wait for the remote
            // for the content stream: 
            InitialRemoteWait = true,
        };
        
        this.chatThread?.Blocks.Add(new ContentBlock
        {
            Time = time,
            ContentType = ContentType.TEXT,
            Role = ChatRole.AI,
            Content = aiText,
        });
        
        // Clear the input field:
        await this.inputField.Clear();
        this.userInput = string.Empty;
        
        // Enable the stream state for the chat component:
        this.isStreaming = true;
        this.hasUnsavedChanges = true;
        this.StateHasChanged();
        
        // Use the selected provider to get the AI response.
        // By awaiting this line, we wait for the entire
        // content to be streamed.
        await aiText.CreateFromProviderAsync(this.providerSettings.CreateProvider(), this.JsRuntime, this.SettingsManager, this.providerSettings.Model, this.chatThread);
        
        // Save the chat:
        if (this.SettingsManager.ConfigurationData.WorkspaceStorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY)
        {
            await this.SaveThread();
            this.hasUnsavedChanges = false;
        }

        // Disable the stream state:
        this.isStreaming = false;
        this.StateHasChanged();
    }

    private async Task InputKeyEvent(KeyboardEventArgs keyEvent)
    {
        this.hasUnsavedChanges = true;
        var key = keyEvent.Code.ToLowerInvariant();
        
        // Was the enter key (either enter or numpad enter) pressed?
        var isEnter = key is "enter" or "numpadenter";
        
        // Was a modifier key pressed as well?
        var isModifier = keyEvent.AltKey || keyEvent.CtrlKey || keyEvent.MetaKey || keyEvent.ShiftKey;
        
        // Depending on the user's settings, might react to shortcuts:
        switch (this.SettingsManager.ConfigurationData.ShortcutSendBehavior)
        {
            case SendBehavior.ENTER_IS_SENDING:
                if (!isModifier && isEnter)
                    await this.SendMessage();
                break;
            
            case SendBehavior.MODIFER_ENTER_IS_SENDING:
                if (isEnter && isModifier)
                    await this.SendMessage();
                break;
        }
    }
    
    private void ToggleWorkspaces()
    {
        this.workspacesVisible = !this.workspacesVisible;
    }
    
    private async Task SaveThread()
    {
        if(this.workspaces is null)
            return;
        
        if(this.chatThread is null)
            return;
        
        if (!this.CanThreadBeSaved)
            return;
        
        await this.workspaces.StoreChat(this.chatThread);
        this.hasUnsavedChanges = false;
    }
    
    private string ExtractThreadName(string firstUserInput)
    {
        // We select the first 10 words of the user input:
        var words = firstUserInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var threadName = string.Join(' ', words.Take(10));
        
        // If the thread name is empty, we use a default name:
        if (string.IsNullOrWhiteSpace(threadName))
            threadName = "Thread";
        
        return threadName;
    }

    private async Task StartNewChat(bool useSameWorkspace = false, bool deletePreviousChat = false)
    {
        if (this.SettingsManager.ConfigurationData.WorkspaceStorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_MANUALLY && this.hasUnsavedChanges)
        {
            var dialogParameters = new DialogParameters
            {
                { "Message", "Are you sure you want to start a new chat? All unsaved changes will be lost." },
            };
        
            var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>("Delete Chat", dialogParameters, DialogOptions.FULLSCREEN);
            var dialogResult = await dialogReference.Result;
            if (dialogResult is null || dialogResult.Canceled)
                return;
        }

        if (this.chatThread is not null && this.workspaces is not null && deletePreviousChat)
        {
            string chatPath;
            if (this.chatThread.WorkspaceId == Guid.Empty)
            {
                chatPath = Path.Join(SettingsManager.DataDirectory, "tempChats", this.chatThread.ChatId.ToString());
            }
            else
            {
                chatPath = Path.Join(SettingsManager.DataDirectory, "workspaces", this.chatThread.WorkspaceId.ToString(), this.chatThread.ChatId.ToString());
            }
            
            await this.workspaces.DeleteChat(chatPath, askForConfirmation: false, unloadChat: true);
        }

        this.isStreaming = false;
        this.hasUnsavedChanges = false;
        this.userInput = string.Empty;

        if (!useSameWorkspace)
        {
            this.chatThread = null;
            this.currentWorkspaceId = Guid.Empty;
            this.currentWorkspaceName = string.Empty;
        }
        else
        {
            this.chatThread = new()
            {
                WorkspaceId = this.currentWorkspaceId,
                ChatId = Guid.NewGuid(),
                Name = string.Empty,
                Seed = this.RNG.Next(),
                SystemPrompt = "You are a helpful assistant!",
                Blocks = [],
            };
        }

        await this.inputField.Clear();
    }

    private async Task MoveChatToWorkspace()
    {
        if(this.chatThread is null)
            return;
        
        if(this.workspaces is null)
            return;
        
        if (this.SettingsManager.ConfigurationData.WorkspaceStorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_MANUALLY && this.hasUnsavedChanges)
        {
            var confirmationDialogParameters = new DialogParameters
            {
                { "Message", "Are you sure you want to move this chat? All unsaved changes will be lost." },
            };
        
            var confirmationDialogReference = await this.DialogService.ShowAsync<ConfirmDialog>("Unsaved Changes", confirmationDialogParameters, DialogOptions.FULLSCREEN);
            var confirmationDialogResult = await confirmationDialogReference.Result;
            if (confirmationDialogResult is null || confirmationDialogResult.Canceled)
                return;
        }
        
        var dialogParameters = new DialogParameters
        {
            { "Message", "Please select the workspace where you want to move the chat to." },
            { "SelectedWorkspace", this.chatThread?.WorkspaceId },
            { "ConfirmText", "Move chat" },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<WorkspaceSelectionDialog>("Move Chat to Workspace", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var workspaceId = dialogResult.Data is Guid id ? id : default;
        if (workspaceId == Guid.Empty)
            return;
        
        // Delete the chat from the current workspace or the temporary storage:
        if (this.chatThread!.WorkspaceId == Guid.Empty)
        {
            // Case: The chat is stored in the temporary storage:
            await this.workspaces.DeleteChat(Path.Join(SettingsManager.DataDirectory, "tempChats", this.chatThread.ChatId.ToString()), askForConfirmation: false, unloadChat: false);
        }
        else
        {
            // Case: The chat is stored in a workspace.
            await this.workspaces.DeleteChat(Path.Join(SettingsManager.DataDirectory, "workspaces", this.chatThread.WorkspaceId.ToString(), this.chatThread.ChatId.ToString()), askForConfirmation: false, unloadChat: false);
        }
        
        this.chatThread!.WorkspaceId = workspaceId;
        await this.SaveThread();
        
        this.currentWorkspaceId = this.chatThread.WorkspaceId;
        this.currentWorkspaceName = await this.workspaces.LoadWorkspaceName(this.chatThread.WorkspaceId);
    }

    private async Task LoadedChatChanged()
    {
        if(this.workspaces is null)
            return;
        
        this.isStreaming = false;
        this.hasUnsavedChanges = false;
        this.userInput = string.Empty;
        this.currentWorkspaceId = this.chatThread?.WorkspaceId ?? Guid.Empty;
        this.currentWorkspaceName = this.chatThread is null ? string.Empty : await this.workspaces.LoadWorkspaceName(this.chatThread.WorkspaceId);
        
        await this.inputField.Clear();
    }

    private void ResetState()
    {
        this.isStreaming = false;
        this.hasUnsavedChanges = false;
        this.userInput = string.Empty;
        this.currentWorkspaceId = Guid.Empty;
        this.currentWorkspaceName = string.Empty;
        this.chatThread = null;
    }

    #region Overrides of MSGComponentBase

    public override Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.RESET_CHAT_STATE:
                this.ResetState();
                break;
        }
        
        return Task.CompletedTask;
    }

    public override Task<TResult?> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data) where TResult : default where TPayload : default
    {
        switch (triggeredEvent)
        {
            case Event.HAS_CHAT_UNSAVED_CHANGES:
                if(this.SettingsManager.ConfigurationData.WorkspaceStorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY)
                    return Task.FromResult((TResult?) (object) false);
                
                return Task.FromResult((TResult?)(object)this.hasUnsavedChanges);
        }
        
        return Task.FromResult(default(TResult));
    }

    #endregion

    #region Implementation of IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        if(this.SettingsManager.ConfigurationData.WorkspaceStorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY)
        {
            await this.SaveThread();
            this.hasUnsavedChanges = false;
        }
    }

    #endregion
}