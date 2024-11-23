using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Dialogs;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Pages;

/// <summary>
/// The chat page.
/// </summary>
public partial class Chat : MSGComponentBase, IAsyncDisposable
{
    [Inject]
    private SettingsManager SettingsManager { get; init; } = null!;

    [Inject]
    private ThreadSafeRandom RNG { get; init; } = null!;
    
    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    private ILogger<Chat> Logger { get; init; } = null!;

    private InnerScrolling scrollingArea = null!;

    private const Placement TOOLBAR_TOOLTIP_PLACEMENT = Placement.Bottom;
    private static readonly Dictionary<string, object?> USER_INPUT_ATTRIBUTES = new();
    
    private AIStudio.Settings.Provider providerSettings;
    private Profile currentProfile = Profile.NO_PROFILE;
    private ChatThread? chatThread;
    private bool hasUnsavedChanges;
    private bool isStreaming;
    private string userInput = string.Empty;
    private string currentWorkspaceName = string.Empty;
    private Guid currentWorkspaceId = Guid.Empty;
    private bool workspaceOverlayVisible;
    private Workspaces? workspaces;
    private bool mustScrollToBottomAfterRender;
    private bool mustStoreChat;
    private bool mustLoadChat;
    private LoadChat loadChat;
    private byte scrollRenderCountdown;
    private bool autoSaveEnabled;
    
    // Unfortunately, we need the input field reference to blur the focus away. Without
    // this, we cannot clear the input field.
    private MudTextField<string> inputField = null!;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.HAS_CHAT_UNSAVED_CHANGES, Event.RESET_CHAT_STATE, Event.CHAT_STREAMING_DONE ]);
        
        // Configure the spellchecking for the user input:
        this.SettingsManager.InjectSpellchecking(USER_INPUT_ATTRIBUTES);

        this.providerSettings = this.SettingsManager.GetPreselectedProvider(Tools.Components.CHAT);
        this.currentProfile = this.SettingsManager.GetPreselectedProfile(Tools.Components.CHAT);
        var deferredContent = MessageBus.INSTANCE.CheckDeferredMessages<ChatThread>(Event.SEND_TO_CHAT).FirstOrDefault();
        if (deferredContent is not null)
        {
            this.chatThread = deferredContent;
            if (this.chatThread is not null)
            {
                if (string.IsNullOrWhiteSpace(this.chatThread.Name))
                {
                    var firstUserBlock = this.chatThread.Blocks.FirstOrDefault(x => x.Role == ChatRole.USER);
                    if (firstUserBlock is not null)
                    {
                        this.chatThread.Name = firstUserBlock.Content switch
                        {
                            ContentText textBlock => this.ExtractThreadName(textBlock.Text),
                            _ => "Thread"
                        };
                    }
                }

                if (this.SettingsManager.ConfigurationData.Workspace.StorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY)
                {
                    this.autoSaveEnabled = true;
                    this.mustStoreChat = true;
                }
            }
            
            if (this.SettingsManager.ConfigurationData.Chat.ShowLatestMessageAfterLoading)
            {
                this.mustScrollToBottomAfterRender = true;
                this.scrollRenderCountdown = 2;
                this.StateHasChanged();
            }
        }
        
        var deferredLoading = MessageBus.INSTANCE.CheckDeferredMessages<LoadChat>(Event.LOAD_CHAT).FirstOrDefault();
        if (deferredLoading != default)
        {
            this.loadChat = deferredLoading;
            this.mustLoadChat = true;
        }

        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && this.chatThread is not null && this.mustStoreChat)
        {
            this.mustStoreChat = false;
            await WorkspaceBehaviour.StoreChat(this.chatThread);
            this.currentWorkspaceId = this.chatThread.WorkspaceId;
            this.currentWorkspaceName = await WorkspaceBehaviour.LoadWorkspaceName(this.chatThread.WorkspaceId);
        }
        
        if (firstRender && this.mustLoadChat)
        {
            this.mustLoadChat = false;
            this.chatThread = await WorkspaceBehaviour.LoadChat(this.loadChat);
            
            if(this.chatThread is not null)
                this.currentWorkspaceName = await WorkspaceBehaviour.LoadWorkspaceName(this.chatThread.WorkspaceId);
            
            this.StateHasChanged();
        }
        
        if(this.mustScrollToBottomAfterRender)
        {
            if (--this.scrollRenderCountdown == 0)
            {
                await this.scrollingArea.ScrollToBottom();
                this.mustScrollToBottomAfterRender = false;
            }
            else
            {
                this.StateHasChanged();
            }
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    #endregion

    private bool IsProviderSelected => this.providerSettings.UsedLLMProvider != LLMProviders.NONE;
    
    private string ProviderPlaceholder => this.IsProviderSelected ? "Type your input here..." : "Select a provider first";

    private string InputLabel => this.IsProviderSelected ? $"Your Prompt (use selected instance '{this.providerSettings.InstanceName}', provider '{this.providerSettings.UsedLLMProvider.ToName()}')" : "Select a provider first";
    
    private bool CanThreadBeSaved => this.chatThread is not null && this.chatThread.Blocks.Count > 0;

    private string TooltipAddChatToWorkspace => $"Start new chat in workspace \"{this.currentWorkspaceName}\"";

    private string UserInputStyle => this.SettingsManager.ConfigurationData.LLMProviders.ShowProviderConfidence ? this.providerSettings.UsedLLMProvider.GetConfidence(this.SettingsManager).SetColorStyle(this.SettingsManager) : string.Empty;
    
    private string UserInputClass => this.SettingsManager.ConfigurationData.LLMProviders.ShowProviderConfidence ? "confidence-border" : string.Empty;
    
    private string WorkspaceSidebarToggleIcon => this.SettingsManager.ConfigurationData.Workspace.IsSidebarVisible ? Icons.Material.Filled.ArrowCircleLeft : Icons.Material.Filled.ArrowCircleRight;

    private void ProfileWasChanged(Profile profile)
    {
        this.currentProfile = profile;
        if(this.chatThread is null)
            return;

        this.chatThread = this.chatThread with
        {
            SystemPrompt = $"""
                            {SystemPrompts.DEFAULT}

                            {this.currentProfile.ToSystemPrompt()}
                            """
        };
    }
    
    private async Task SendMessage()
    {
        if (!this.IsProviderSelected)
            return;
        
        // We need to blur the focus away from the input field
        // to be able to clear the field:
        await this.inputField.BlurAsync();
        
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
                SystemPrompt = $"""
                                {SystemPrompts.DEFAULT}

                                {this.currentProfile.ToSystemPrompt()}
                                """,
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
        if (this.SettingsManager.ConfigurationData.Workspace.StorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY)
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
        this.userInput = string.Empty;
        
        // Enable the stream state for the chat component:
        this.isStreaming = true;
        this.hasUnsavedChanges = true;
        
        if (this.SettingsManager.ConfigurationData.Chat.ShowLatestMessageAfterLoading)
        {
            this.mustScrollToBottomAfterRender = true;
            this.scrollRenderCountdown = 2;
        }
        
        this.StateHasChanged();
        
        // Use the selected provider to get the AI response.
        // By awaiting this line, we wait for the entire
        // content to be streamed.
        await aiText.CreateFromProviderAsync(this.providerSettings.CreateProvider(this.Logger), this.SettingsManager, this.providerSettings.Model, this.chatThread);
        
        // Save the chat:
        if (this.SettingsManager.ConfigurationData.Workspace.StorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY)
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
        switch (this.SettingsManager.ConfigurationData.Chat.ShortcutSendBehavior)
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
    
    private void ToggleWorkspaceOverlay()
    {
        this.workspaceOverlayVisible = !this.workspaceOverlayVisible;
    }

    private async Task ToggleWorkspaceSidebar()
    {
        this.SettingsManager.ConfigurationData.Workspace.IsSidebarVisible = !this.SettingsManager.ConfigurationData.Workspace.IsSidebarVisible;
        await this.SettingsManager.StoreSettings();
    }
    
    private async Task SaveThread()
    {
        if(this.chatThread is null)
            return;
        
        if (!this.CanThreadBeSaved)
            return;

        //
        // When the workspace component is visible, we store the chat
        // through the workspace component. The advantage of this is that
        // the workspace gets updated automatically when the chat is saved.
        //
        if (this.workspaces is not null)
            await this.workspaces.StoreChat(this.chatThread);
        else
            await WorkspaceBehaviour.StoreChat(this.chatThread);
        
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
        if (this.SettingsManager.ConfigurationData.Workspace.StorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_MANUALLY && this.hasUnsavedChanges)
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
                SystemPrompt = $"""
                                {SystemPrompts.DEFAULT}

                                {this.currentProfile.ToSystemPrompt()}
                                """,
                Blocks = [],
            };
        }

        this.userInput = string.Empty;
    }

    private async Task MoveChatToWorkspace()
    {
        if(this.chatThread is null)
            return;
        
        if(this.workspaces is null)
            return;
        
        if (this.SettingsManager.ConfigurationData.Workspace.StorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_MANUALLY && this.hasUnsavedChanges)
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
        this.currentWorkspaceName = await WorkspaceBehaviour.LoadWorkspaceName(this.chatThread.WorkspaceId);
    }

    private async Task LoadedChatChanged()
    {
        if(this.workspaces is null)
            return;
        
        this.isStreaming = false;
        this.hasUnsavedChanges = false;
        this.userInput = string.Empty;
        this.currentWorkspaceId = this.chatThread?.WorkspaceId ?? Guid.Empty;
        this.currentWorkspaceName = this.chatThread is null ? string.Empty : await WorkspaceBehaviour.LoadWorkspaceName(this.chatThread.WorkspaceId);

        this.userInput = string.Empty;
        if (this.SettingsManager.ConfigurationData.Chat.ShowLatestMessageAfterLoading)
        {
            this.mustScrollToBottomAfterRender = true;
            this.scrollRenderCountdown = 2;
            this.StateHasChanged();
        }
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

    public override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.RESET_CHAT_STATE:
                this.ResetState();
                break;
            
            case Event.CHAT_STREAMING_DONE:
                if(this.autoSaveEnabled)
                    await this.SaveThread();
                break;
        }
    }

    public override Task<TResult?> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data) where TResult : default where TPayload : default
    {
        switch (triggeredEvent)
        {
            case Event.HAS_CHAT_UNSAVED_CHANGES:
                if(this.SettingsManager.ConfigurationData.Workspace.StorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY)
                    return Task.FromResult((TResult?) (object) false);
                
                return Task.FromResult((TResult?)(object)this.hasUnsavedChanges);
        }
        
        return Task.FromResult(default(TResult));
    }

    #endregion

    #region Implementation of IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        if(this.SettingsManager.ConfigurationData.Workspace.StorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY)
        {
            await this.SaveThread();
            this.hasUnsavedChanges = false;
        }
    }

    #endregion
}