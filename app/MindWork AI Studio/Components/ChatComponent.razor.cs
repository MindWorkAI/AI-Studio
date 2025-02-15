using AIStudio.Chat;
using AIStudio.Dialogs;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

public partial class ChatComponent : MSGComponentBase, IAsyncDisposable
{
    [Parameter]
    public ChatThread? ChatThread { get; set; }
    
    [Parameter]
    public EventCallback<ChatThread?> ChatThreadChanged { get; set; }
    
    [Parameter]
    public AIStudio.Settings.Provider Provider { get; set; }
    
    [Parameter]
    public EventCallback<AIStudio.Settings.Provider> ProviderChanged { get; set; }
    
    [Parameter]
    public Action<string> WorkspaceName { get; set; } = _ => { };
    
    [Parameter]
    public Workspaces? Workspaces { get; set; }
    
    [Inject]
    private ILogger<ChatComponent> Logger { get; set; } = null!;
    
    [Inject]
    private ThreadSafeRandom RNG { get; init; } = null!;
    
    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    private DataSourceService DataSourceService { get; init; } = null!;
    
    private const Placement TOOLBAR_TOOLTIP_PLACEMENT = Placement.Top;
    private static readonly Dictionary<string, object?> USER_INPUT_ATTRIBUTES = new();

    private DataSourceSelection? dataSourceSelectionComponent;
    private DataSourceOptions earlyDataSourceOptions = new();
    private Profile currentProfile = Profile.NO_PROFILE;
    private bool hasUnsavedChanges;
    private bool mustScrollToBottomAfterRender;
    private InnerScrolling scrollingArea = null!;
    private byte scrollRenderCountdown;
    private bool isStreaming;
    private string userInput = string.Empty;
    private bool mustStoreChat;
    private bool mustLoadChat;
    private LoadChat loadChat;
    private bool autoSaveEnabled;
    private string currentWorkspaceName = string.Empty;
    private Guid currentWorkspaceId = Guid.Empty;
    private CancellationTokenSource? cancellationTokenSource;
    
    // Unfortunately, we need the input field reference to blur the focus away. Without
    // this, we cannot clear the input field.
    private MudTextField<string> inputField = null!;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Apply the filters for the message bus:
        this.ApplyFilters([], [ Event.HAS_CHAT_UNSAVED_CHANGES, Event.RESET_CHAT_STATE, Event.CHAT_STREAMING_DONE, Event.WORKSPACE_LOADED_CHAT_CHANGED ]);
        
        // Configure the spellchecking for the user input:
        this.SettingsManager.InjectSpellchecking(USER_INPUT_ATTRIBUTES);

        // Get the preselected profile:
        this.currentProfile = this.SettingsManager.GetPreselectedProfile(Tools.Components.CHAT);
        
        //
        // Check for deferred messages of the kind 'SEND_TO_CHAT',
        // aka the user sends an assistant result to the chat:
        //
        var deferredContent = MessageBus.INSTANCE.CheckDeferredMessages<ChatThread>(Event.SEND_TO_CHAT).FirstOrDefault();
        if (deferredContent is not null)
        {
            //
            // Yes, the user sent an assistant result to the chat.
            //
            
            // Use chat thread sent by the user:
            this.ChatThread = deferredContent;
            this.Logger.LogInformation($"The chat '{this.ChatThread.Name}' with {this.ChatThread.Blocks.Count} messages was deferred and will be rendered now.");
            await this.ChatThreadChanged.InvokeAsync(this.ChatThread);
            
            // We know already that the chat thread is not null,
            // but we have to check it again for the nullability
            // for the compiler:
            if (this.ChatThread is not null)
            {
                //
                // Check if the chat thread has a name. If not, we
                // generate the name now:
                //
                if (string.IsNullOrWhiteSpace(this.ChatThread.Name))
                {
                    var firstUserBlock = this.ChatThread.Blocks.FirstOrDefault(x => x.Role == ChatRole.USER);
                    if (firstUserBlock is not null)
                    {
                        this.ChatThread.Name = firstUserBlock.Content switch
                        {
                            ContentText textBlock => this.ExtractThreadName(textBlock.Text),
                            _ => "Thread"
                        };
                    }
                }

                //
                // Check if the user wants to apply the standard chat data source options:
                //
                if (this.SettingsManager.ConfigurationData.Chat.SendToChatDataSourceBehavior is SendToChatDataSourceBehavior.APPLY_STANDARD_CHAT_DATA_SOURCE_OPTIONS)
                    this.ChatThread.DataSourceOptions = this.SettingsManager.ConfigurationData.Chat.PreselectedDataSourceOptions.CreateCopy();

                //
                // Check if the user wants to store the chat automatically:
                //
                if (this.SettingsManager.ConfigurationData.Workspace.StorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY)
                {
                    this.autoSaveEnabled = true;
                    this.mustStoreChat = true;
                    
                    //
                    // When a standard workspace is used, we have to ensure
                    // that the workspace is available:
                    //
                    if(this.ChatThread.WorkspaceId == KnownWorkspaces.ERI_SERVER_WORKSPACE_ID)
                        await WorkspaceBehaviour.EnsureERIServerWorkspace();
                    
                    else if (this.ChatThread.WorkspaceId == KnownWorkspaces.BIAS_WORKSPACE_ID)
                        await WorkspaceBehaviour.EnsureBiasWorkspace();
                }
            }
        }
        else
        {
            //
            // No, the user did not send an assistant result to the chat.
            //
            this.ApplyStandardDataSourceOptions();
        }
        
        //
        // Check if the user wants to show the latest message after loading:
        //
        if (this.SettingsManager.ConfigurationData.Chat.ShowLatestMessageAfterLoading)
        {
            //
            // We cannot scroll to the bottom right now because the
            // chat component is not rendered yet. We have to wait for
            // the rendering process to finish. Thus, we set a flag
            // to scroll to the bottom after the rendering process.:
            //
            this.mustScrollToBottomAfterRender = true;
            this.scrollRenderCountdown = 4;
            this.StateHasChanged();
        }
        
        //
        // Check if another component deferred the loading of a chat.
        //
        // This is used, e.g., for the bias-of-the-day component:
        // when the bias for this day was already produced, the bias
        // component sends a message to the chat component to load
        // the chat with the bias:
        //
        var deferredLoading = MessageBus.INSTANCE.CheckDeferredMessages<LoadChat>(Event.LOAD_CHAT).FirstOrDefault();
        if (deferredLoading != default)
        {
            this.loadChat = deferredLoading;
            this.mustLoadChat = true;
            this.Logger.LogInformation($"The loading of the chat '{this.loadChat.ChatId}' was deferred and will be loaded now.");
        }

        //
        // When for whatever reason we have a chat thread, we have to
        // ensure that the corresponding workspace id is set and the
        // workspace name is loaded:
        //
        if (this.ChatThread is not null)
        {
            this.currentWorkspaceId = this.ChatThread.WorkspaceId;
            this.currentWorkspaceName = await WorkspaceBehaviour.LoadWorkspaceName(this.ChatThread.WorkspaceId);
            this.WorkspaceName(this.currentWorkspaceName);
        }
        
        // Select the correct provider:
        await this.SelectProviderWhenLoadingChat();
        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && this.ChatThread is not null && this.mustStoreChat)
        {
            this.mustStoreChat = false;
            
            if(this.Workspaces is not null)
                await this.Workspaces.StoreChat(this.ChatThread);
            else
                await WorkspaceBehaviour.StoreChat(this.ChatThread);
            
            this.currentWorkspaceId = this.ChatThread.WorkspaceId;
            this.currentWorkspaceName = await WorkspaceBehaviour.LoadWorkspaceName(this.ChatThread.WorkspaceId);
            this.WorkspaceName(this.currentWorkspaceName);
        }
        
        if (firstRender && this.mustLoadChat)
        {
            this.Logger.LogInformation($"Try to load the chat '{this.loadChat.ChatId}' now.");
            this.mustLoadChat = false;
            this.ChatThread = await WorkspaceBehaviour.LoadChat(this.loadChat);
            
            if(this.ChatThread is not null)
            {
                await this.ChatThreadChanged.InvokeAsync(this.ChatThread);
                this.Logger.LogInformation($"The chat '{this.ChatThread!.ChatId}' with title '{this.ChatThread.Name}' ({this.ChatThread.Blocks.Count} messages) was loaded successfully.");
                
                this.currentWorkspaceName = await WorkspaceBehaviour.LoadWorkspaceName(this.ChatThread.WorkspaceId);
                this.WorkspaceName(this.currentWorkspaceName);
                await this.SelectProviderWhenLoadingChat();
            }
            else
                this.Logger.LogWarning($"The chat '{this.loadChat.ChatId}' could not be loaded.");

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
    
    private bool IsProviderSelected => this.Provider.UsedLLMProvider != LLMProviders.NONE;
    
    private string ProviderPlaceholder => this.IsProviderSelected ? "Type your input here..." : "Select a provider first";

    private string InputLabel => this.IsProviderSelected ? $"Your Prompt (use selected instance '{this.Provider.InstanceName}', provider '{this.Provider.UsedLLMProvider.ToName()}')" : "Select a provider first";
    
    private bool CanThreadBeSaved => this.ChatThread is not null && this.ChatThread.Blocks.Count > 0;
    
    private string TooltipAddChatToWorkspace => $"Start new chat in workspace \"{this.currentWorkspaceName}\"";

    private string UserInputStyle => this.SettingsManager.ConfigurationData.LLMProviders.ShowProviderConfidence ? this.Provider.UsedLLMProvider.GetConfidence(this.SettingsManager).SetColorStyle(this.SettingsManager) : string.Empty;
    
    private string UserInputClass => this.SettingsManager.ConfigurationData.LLMProviders.ShowProviderConfidence ? "confidence-border" : string.Empty;
    
    private void ApplyStandardDataSourceOptions()
    {
        var chatDefaultOptions = this.SettingsManager.ConfigurationData.Chat.PreselectedDataSourceOptions.CreateCopy();
        this.earlyDataSourceOptions = chatDefaultOptions;
        this.dataSourceSelectionComponent?.ChangeOptionWithoutSaving(chatDefaultOptions);
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
    
    private async Task ProfileWasChanged(Profile profile)
    {
        this.currentProfile = profile;
        if(this.ChatThread is null)
            return;

        this.ChatThread = this.ChatThread with
        {
            SelectedProfile = this.currentProfile.Id,
        };
        
        await this.ChatThreadChanged.InvokeAsync(this.ChatThread);
    }

    private DataSourceOptions GetCurrentDataSourceOptions()
    {
        if (this.ChatThread is not null)
            return this.ChatThread.DataSourceOptions;
        
        return this.earlyDataSourceOptions;
    }
    
    private async Task SetCurrentDataSourceOptions(DataSourceOptions updatedOptions)
    {
        if (this.ChatThread is not null)
        {
            this.hasUnsavedChanges = true;
            this.ChatThread.DataSourceOptions = updatedOptions;
            if(this.SettingsManager.ConfigurationData.Workspace.StorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY)
            {
                await this.SaveThread();
                this.hasUnsavedChanges = false;
            }
        }
        else
            this.earlyDataSourceOptions = updatedOptions;
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
    
    private async Task SendMessage(bool reuseLastUserPrompt = false)
    {
        if (!this.IsProviderSelected)
            return;
        
        // We need to blur the focus away from the input field
        // to be able to clear the field:
        await this.inputField.BlurAsync();
        
        // Create a new chat thread if necessary:
        if (this.ChatThread is null)
        {
            this.ChatThread = new()
            {
                SelectedProvider = this.Provider.Id,
                SelectedProfile = this.currentProfile.Id,
                SystemPrompt = SystemPrompts.DEFAULT,
                WorkspaceId = this.currentWorkspaceId,
                ChatId = Guid.NewGuid(),
                DataSourceOptions = this.earlyDataSourceOptions,
                Name = this.ExtractThreadName(this.userInput),
                Seed = this.RNG.Next(),
                Blocks = [],
            };
            
            await this.ChatThreadChanged.InvokeAsync(this.ChatThread);
        }
        else
        {
            // Set the thread name if it is empty:
            if (string.IsNullOrWhiteSpace(this.ChatThread.Name))
                this.ChatThread.Name = this.ExtractThreadName(this.userInput);
            
            // Update provider and profile:
            this.ChatThread.SelectedProvider = this.Provider.Id;
            this.ChatThread.SelectedProfile = this.currentProfile.Id;
        }

        var time = DateTimeOffset.Now;
        IContent? lastUserPrompt;
        if (!reuseLastUserPrompt)
        {
            lastUserPrompt = new ContentText
            {
                Text = this.userInput,
            };

            //
            // Add the user message to the thread:
            //
            this.ChatThread?.Blocks.Add(new ContentBlock
            {
                Time = time,
                ContentType = ContentType.TEXT,
                Role = ChatRole.USER,
                Content = lastUserPrompt,
            });

            // Save the chat:
            if (this.SettingsManager.ConfigurationData.Workspace.StorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY)
            {
                await this.SaveThread();
                this.hasUnsavedChanges = false;
                this.StateHasChanged();
            }
        }
        else
            lastUserPrompt = this.ChatThread.Blocks.Last(x => x.Role is ChatRole.USER).Content;

        //
        // Add the AI response to the thread:
        //
        var aiText = new ContentText
        {
            // We have to wait for the remote
            // for the content stream: 
            InitialRemoteWait = true,
        };
        
        this.ChatThread?.Blocks.Add(new ContentBlock
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
        
        this.Logger.LogDebug($"Start processing user input using provider '{this.Provider.InstanceName}' with model '{this.Provider.Model}'.");
        
        using (this.cancellationTokenSource = new())
        {
            this.StateHasChanged();
            
            // Use the selected provider to get the AI response.
            // By awaiting this line, we wait for the entire
            // content to be streamed.
            await aiText.CreateFromProviderAsync(this.Provider.CreateProvider(this.Logger), this.SettingsManager, this.DataSourceService, this.Provider.Model, lastUserPrompt, this.ChatThread, this.cancellationTokenSource.Token);
        }
        
        this.cancellationTokenSource = null;

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
    
    private async Task CancelStreaming()
    {
        if (this.cancellationTokenSource is not null)
            if(!this.cancellationTokenSource.IsCancellationRequested)
                await this.cancellationTokenSource.CancelAsync();
    }
    
    private async Task SaveThread()
    {
        if(this.ChatThread is null)
            return;
        
        if (!this.CanThreadBeSaved)
            return;

        //
        // When the workspace component is visible, we store the chat
        // through the workspace component. The advantage of this is that
        // the workspace gets updated automatically when the chat is saved.
        //
        if (this.Workspaces is not null)
            await this.Workspaces.StoreChat(this.ChatThread);
        else
            await WorkspaceBehaviour.StoreChat(this.ChatThread);
        
        this.hasUnsavedChanges = false;
    }
    
    private async Task StartNewChat(bool useSameWorkspace = false, bool deletePreviousChat = false)
    {
        //
        // Want the user to manage the chat storage manually? In that case, we have to ask the user
        // about possible data loss:
        //
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

        //
        // Delete the previous chat when desired and necessary:
        //
        if (this.ChatThread is not null && deletePreviousChat)
        {
            string chatPath;
            if (this.ChatThread.WorkspaceId == Guid.Empty)
                chatPath = Path.Join(SettingsManager.DataDirectory, "tempChats", this.ChatThread.ChatId.ToString());
            else
                chatPath = Path.Join(SettingsManager.DataDirectory, "workspaces", this.ChatThread.WorkspaceId.ToString(), this.ChatThread.ChatId.ToString());

            if(this.Workspaces is null)
                await WorkspaceBehaviour.DeleteChat(this.DialogService, this.ChatThread.WorkspaceId, this.ChatThread.ChatId, askForConfirmation: false);
            else
                await this.Workspaces.DeleteChat(chatPath, askForConfirmation: false, unloadChat: true);
        }

        //
        // Reset our state:
        //
        this.isStreaming = false;
        this.hasUnsavedChanges = false;
        this.userInput = string.Empty;
        
        //
        // Reset the LLM provider considering the user's settings:
        //
        switch (this.SettingsManager.ConfigurationData.Chat.AddChatProviderBehavior)
        {
            case AddChatProviderBehavior.ADDED_CHATS_USE_DEFAULT_PROVIDER:
                this.Provider = this.SettingsManager.GetPreselectedProvider(Tools.Components.CHAT);
                await this.ProviderChanged.InvokeAsync(this.Provider);
                break;
            
            default:
            case AddChatProviderBehavior.ADDED_CHATS_USE_LATEST_PROVIDER:
                if(this.Provider == default)
                {
                    this.Provider = this.SettingsManager.GetPreselectedProvider(Tools.Components.CHAT);
                    await this.ProviderChanged.InvokeAsync(this.Provider);
                }

                break;
        }

        //
        // Reset the chat thread or create a new one:
        //
        if (!useSameWorkspace)
        {
            //
            // When the user wants to start a new chat outside the current workspace,
            // we have to reset the workspace id and the workspace name. Also, we have
            // to reset the chat thread:
            //
            this.ChatThread = null;
            this.currentWorkspaceId = Guid.Empty;
            this.currentWorkspaceName = string.Empty;
            this.WorkspaceName(this.currentWorkspaceName);
        }
        else
        {
            //
            // When the user wants to start a new chat in the same workspace, we have to
            // reset the chat thread only. The workspace id and the workspace name remain
            // the same:
            //
            this.ChatThread = new()
            {
                SelectedProvider = this.Provider.Id,
                SelectedProfile = this.currentProfile.Id,
                SystemPrompt = SystemPrompts.DEFAULT,
                WorkspaceId = this.currentWorkspaceId,
                ChatId = Guid.NewGuid(),
                Name = string.Empty,
                Seed = this.RNG.Next(),
                Blocks = [],
            };
        }
        
        // Now, we have to reset the data source options as well:
        this.ApplyStandardDataSourceOptions();
        
        // Notify the parent component about the change:
        await this.ChatThreadChanged.InvokeAsync(this.ChatThread);
    }
    
    private async Task MoveChatToWorkspace()
    {
        if(this.ChatThread is null)
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
            { "SelectedWorkspace", this.ChatThread?.WorkspaceId },
            { "ConfirmText", "Move chat" },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<WorkspaceSelectionDialog>("Move Chat to Workspace", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var workspaceId = dialogResult.Data is Guid id ? id : Guid.Empty;
        if (workspaceId == Guid.Empty)
            return;
        
        // Delete the chat from the current workspace or the temporary storage:
        await WorkspaceBehaviour.DeleteChat(this.DialogService, this.ChatThread!.WorkspaceId, this.ChatThread.ChatId, askForConfirmation: false);
        
        this.ChatThread!.WorkspaceId = workspaceId;
        await this.SaveThread();
        
        this.currentWorkspaceId = this.ChatThread.WorkspaceId;
        this.currentWorkspaceName = await WorkspaceBehaviour.LoadWorkspaceName(this.ChatThread.WorkspaceId);
        this.WorkspaceName(this.currentWorkspaceName);
    }
    
    private async Task LoadedChatChanged()
    {
        this.isStreaming = false;
        this.hasUnsavedChanges = false;
        this.userInput = string.Empty;

        if (this.ChatThread is not null)
        {
            this.currentWorkspaceId = this.ChatThread.WorkspaceId;
            this.currentWorkspaceName = await WorkspaceBehaviour.LoadWorkspaceName(this.ChatThread.WorkspaceId);
            this.WorkspaceName(this.currentWorkspaceName);
            this.dataSourceSelectionComponent?.ChangeOptionWithoutSaving(this.ChatThread.DataSourceOptions);
        }
        else
        {
            this.currentWorkspaceId = Guid.Empty;
            this.currentWorkspaceName = string.Empty;
            this.WorkspaceName(this.currentWorkspaceName);
            this.ApplyStandardDataSourceOptions();
        }
        
        await this.SelectProviderWhenLoadingChat();
        if (this.SettingsManager.ConfigurationData.Chat.ShowLatestMessageAfterLoading)
        {
            this.mustScrollToBottomAfterRender = true;
            this.scrollRenderCountdown = 2;
        }
        
        this.StateHasChanged();
    }
    
    private async Task ResetState()
    {
        this.isStreaming = false;
        this.hasUnsavedChanges = false;
        this.userInput = string.Empty;
        this.currentWorkspaceId = Guid.Empty;
        
        this.currentWorkspaceName = string.Empty;
        this.WorkspaceName(this.currentWorkspaceName);
        
        this.ChatThread = null;
        this.ApplyStandardDataSourceOptions();
        await this.ChatThreadChanged.InvokeAsync(this.ChatThread);
    }
    
    private async Task SelectProviderWhenLoadingChat()
    {
        var chatProvider = this.ChatThread?.SelectedProvider;
        var chatProfile = this.ChatThread?.SelectedProfile;

        switch (this.SettingsManager.ConfigurationData.Chat.LoadingProviderBehavior)
        {
            default:
            case LoadingChatProviderBehavior.USE_CHAT_PROVIDER_IF_AVAILABLE:
                this.Provider = this.SettingsManager.GetPreselectedProvider(Tools.Components.CHAT, chatProvider);
                break;
            
            case LoadingChatProviderBehavior.ALWAYS_USE_DEFAULT_CHAT_PROVIDER:
                this.Provider = this.SettingsManager.GetPreselectedProvider(Tools.Components.CHAT);
                break;
            
            case LoadingChatProviderBehavior.ALWAYS_USE_LATEST_CHAT_PROVIDER:
                if(this.Provider == default)
                    this.Provider = this.SettingsManager.GetPreselectedProvider(Tools.Components.CHAT);
                break;
        }
        
        await this.ProviderChanged.InvokeAsync(this.Provider);

        // Try to select the profile:
        if (!string.IsNullOrWhiteSpace(chatProfile))
        {
            this.currentProfile = this.SettingsManager.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == chatProfile);
            if(this.currentProfile == default)
                this.currentProfile = Profile.NO_PROFILE;
        }
    }

    private async Task ToggleWorkspaceOverlay()
    {
        await MessageBus.INSTANCE.SendMessage<bool>(this, Event.WORKSPACE_TOGGLE_OVERLAY);
    }
    
    private async Task RemoveBlock(IContent block)
    {
        if(this.ChatThread is null)
            return;
        
        this.ChatThread.Remove(block);
        this.hasUnsavedChanges = true;
        await this.SaveThread();
        this.StateHasChanged();
    }

    private async Task RegenerateBlock(IContent aiBlock)
    {
        if(this.ChatThread is null)
            return;
        
        this.ChatThread.Remove(aiBlock, removeForRegenerate: true);
        this.hasUnsavedChanges = true;
        this.StateHasChanged();
        
        await this.SendMessage(reuseLastUserPrompt: true);
    }
    
    private Task EditLastUserBlock(IContent block)
    {
        if(this.ChatThread is null)
            return Task.CompletedTask;
        
        if (block is not ContentText textBlock)
            return Task.CompletedTask;
        
        var lastBlock = this.ChatThread.Blocks.Last();
        var lastBlockContent = lastBlock.Content;
        if(lastBlockContent is null)
            return Task.CompletedTask;
        
        this.userInput = textBlock.Text;
        this.ChatThread.Remove(block);
        this.ChatThread.Remove(lastBlockContent);
        this.hasUnsavedChanges = true;
        this.StateHasChanged();
        
        return Task.CompletedTask;
    }
    
    private Task EditLastBlock(IContent block)
    {
        if(this.ChatThread is null)
            return Task.CompletedTask;
        
        if (block is not ContentText textBlock)
            return Task.CompletedTask;
        
        this.userInput = textBlock.Text;
        this.ChatThread.Remove(block);
        this.hasUnsavedChanges = true;
        this.StateHasChanged();
        
        return Task.CompletedTask;
    }
    
    #region Overrides of MSGComponentBase

    public override string ComponentName => nameof(ChatComponent);
    
    public override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.RESET_CHAT_STATE:
                await this.ResetState();
                break;
            
            case Event.CHAT_STREAMING_DONE:
                if(this.autoSaveEnabled)
                    await this.SaveThread();
                break;
            
            case Event.WORKSPACE_LOADED_CHAT_CHANGED:
                await this.LoadedChatChanged();
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
        this.MessageBus.Unregister(this);
        if(this.SettingsManager.ConfigurationData.Workspace.StorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY)
        {
            await this.SaveThread();
            this.hasUnsavedChanges = false;
        }

        if (this.cancellationTokenSource is not null)
        {
            if(!this.cancellationTokenSource.IsCancellationRequested)
                await this.cancellationTokenSource.CancelAsync();
            
            this.cancellationTokenSource.Dispose();
        }
    }

    #endregion
}