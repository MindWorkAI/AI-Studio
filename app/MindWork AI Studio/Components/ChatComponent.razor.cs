using AIStudio.Chat;
using AIStudio.Dialogs;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.AIJobs;
using AIStudio.Tools.Media;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

public partial class ChatComponent : MSGComponentBase, IAsyncDisposable
{
    private readonly Guid draftMediaOwnerId = Guid.NewGuid();
    private const string CHAT_INPUT_ID = "chat-user-input";
    private const string MARKDOWN_CODE = "code";
    private const string MARKDOWN_BOLD = "bold";
    private const string MARKDOWN_ITALIC = "italic";
    private const string MARKDOWN_HEADING = "heading";
    private const string MARKDOWN_BULLET_LIST = "bullet_list";
    
    [Parameter]
    public ChatThread? ChatThread { get; set; }
    
    [Parameter]
    public EventCallback<ChatThread?> ChatThreadChanged { get; set; }

    [Parameter]
    public AIStudio.Settings.Provider Provider { get; set; } = AIStudio.Settings.Provider.NONE;
    
    [Parameter]
    public EventCallback<AIStudio.Settings.Provider> ProviderChanged { get; set; }
    
    [Parameter]
    public Action<string> WorkspaceName { get; set; } = _ => { };
    
    [Parameter]
    public Workspaces? Workspaces { get; set; }

    [Parameter]
    public ChatComposerState ComposerState { get; set; } = new();
    
    [Inject]
    private ILogger<ChatComponent> Logger { get; set; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    private IJSRuntime JsRuntime { get; init; } = null!;

    [Inject]
    private AIJobService AIJobService { get; init; } = null!;

    [Inject]
    private MediaTranscriptionService MediaTranscriptionService { get; init; } = null!;

    private const Placement TOOLBAR_TOOLTIP_PLACEMENT = Placement.Top;
    private static readonly Dictionary<string, object?> USER_INPUT_ATTRIBUTES = new();

    private DataSourceSelection? dataSourceSelectionComponent;
    private DataSourceOptions earlyDataSourceOptions = new();
    private DataSourceOptions lastAppliedStandardDataSourceOptions = new();
    private Profile currentProfile = Profile.NO_PROFILE;
    private ChatTemplate currentChatTemplate = ChatTemplate.NO_CHAT_TEMPLATE;
    private bool hasUnsavedChanges;
    private bool mustScrollToBottomAfterRender;
    private InnerScrolling scrollingArea = null!;
    private byte scrollRenderCountdown;
    private bool mustStoreChat;
    private bool mustLoadChat;
    private LoadChat loadChat;
    private bool autoSaveEnabled;
    private bool previousInputForbidden = true;
    private Guid lastSeenChatId = Guid.Empty;
    private AIStudio.Settings.Provider lastSeenProvider = AIStudio.Settings.Provider.NONE;
    private string currentWorkspaceName = string.Empty;
    private Guid currentWorkspaceId = Guid.Empty;
    private Guid currentChatThreadId = Guid.Empty;
    private Guid loadedParameterChatId = Guid.Empty;
    private Guid loadedParameterWorkspaceId = Guid.Empty;
    private Guid foregroundChatId = Guid.Empty;
    private int workspaceHeaderSyncVersion;

    private MediaImportOwner CurrentMediaImportOwner => MediaImportOwner.ForChat(this.ChatThread?.ChatId ?? this.draftMediaOwnerId);

    // Unfortunately, we need the input field reference to blur the focus away. Without
    // this, we cannot clear the input field.
    private MudTextField<string> inputField = null!;

    /// <summary>
    /// Represents the user's input in the chat interface.
    /// </summary>
    /// <remarks>
    /// This property serves as a bridge between the chat component and the
    /// underlying composer state, allowing user input to be dynamically updated
    /// and managed. The setter also triggers state changes within the composer
    /// to track whether the user has drafted any input.
    /// </remarks>
    private string UserInput
    {
        get => this.ComposerState.UserInput;
        set => this.ComposerState.SetUserInput(value);
    }

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.MediaTranscriptionService.StateChanged += this.OnMediaImportStateChanged;
        
        // Apply the filters for the message bus:
        this.ApplyFilters([], [ Event.HAS_CHAT_UNSAVED_CHANGES, Event.RESET_CHAT_STATE, Event.CHAT_STREAMING_DONE, Event.AI_JOB_CHANGED, Event.AI_JOB_FINISHED, Event.CHAT_GENERATION_CHANGED, Event.WORKSPACE_RENAMED, Event.CONFIGURATION_CHANGED ]);
        
        // Configure the spellchecking for the user input:
        this.SettingsManager.InjectSpellchecking(USER_INPUT_ATTRIBUTES);
        USER_INPUT_ATTRIBUTES["id"] = CHAT_INPUT_ID;

        // Get the preselected profile:
        this.currentProfile = this.SettingsManager.GetPreselectedProfile(Tools.Components.CHAT);
        
        // Get the preselected chat template:
        this.currentChatTemplate = this.SettingsManager.GetPreselectedChatTemplate(Tools.Components.CHAT);
        if (!this.ComposerState.HasUserDraft && !this.ComposerState.HasComposerContent)
            this.ComposerState.ApplyTemplate(this.currentChatTemplate);

        this.lastAppliedStandardDataSourceOptions = this.SettingsManager.ConfigurationData.Chat.PreselectedDataSourceOptions.CreateCopy();

        var deferredInput = MessageBus.INSTANCE.CheckDeferredMessages<string>(Event.SEND_TO_CHAT_INPUT).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(deferredInput))
            this.ComposerState.SetUserInput(deferredInput);

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
            this.ChatThread.IncludeDateTime = true;
            
            this.Logger.LogInformation($"The chat '{this.ChatThread.ChatId}' with {this.ChatThread.Blocks.Count} messages was deferred and will be rendered now.");
            this.MarkCurrentChatAsLoadedParameter();
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
            await this.SyncWorkspaceHeaderWithChatThreadAsync();
        
        // Select the correct provider:
        await this.SelectProviderWhenLoadingChat();
        await this.SyncForegroundChatAsync();
        await this.ConsumeMediaOutcomeAsync();
        await base.OnInitializedAsync();
    }

    /// <summary>Refreshes send and attachment controls when the media import lane changes.</summary>
    private void OnMediaImportStateChanged(MediaImportOwner owner)
    {
        if (owner == this.CurrentMediaImportOwner)
            _ = this.InvokeAsync(async () =>
            {
                await this.ConsumeMediaOutcomeAsync();
                this.StateHasChanged();
            });
    }

    /// <summary>Consumes a terminal media notification when its chat is visible.</summary>
    private async Task ConsumeMediaOutcomeAsync()
    {
        var outcome = this.MediaTranscriptionService.TryConsumeOutcome(this.CurrentMediaImportOwner);
        if (outcome is null)
            return;

        if (outcome.Failures.Count > 0)
        {
            var message = string.Join(Environment.NewLine, outcome.Failures.Select(failure => $"{failure.FileName}: {failure.UserMessage}"));
            await this.MessageBus.SendError(new(Icons.Material.Filled.VoiceChat, message));
        }
        else if (outcome.Status is MediaImportStatus.FAILED)
        {
            await this.MessageBus.SendError(new(Icons.Material.Filled.VoiceChat, this.T("The media file could not be transcribed.")));
        }

        if (outcome.Warnings.Count > 0)
        {
            var message = string.Join(Environment.NewLine, outcome.Warnings.Select(warning => $"{warning.FileName}: {warning.UserMessage}"));
            await this.MessageBus.SendWarning(new(Icons.Material.Filled.VoiceChat, message));
        }

        if (outcome.Status is MediaImportStatus.CANCELLED)
        {
            await this.MessageBus.SendWarning(new(Icons.Material.Filled.VoiceChat, this.T("The media transcription was canceled.")));
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && this.ChatThread is not null && this.mustStoreChat)
        {
            this.mustStoreChat = false;
            
            if(this.Workspaces is not null)
                await this.Workspaces.StoreChatAsync(this.ChatThread);
            else
                await WorkspaceBehaviour.StoreChatAsync(this.ChatThread);

            await this.SyncWorkspaceHeaderWithChatThreadAsync();
        }
        
        if (firstRender && this.mustLoadChat)
        {
            this.Logger.LogInformation($"Try to load the chat '{this.loadChat.ChatId}' now.");
            this.mustLoadChat = false;
            this.ChatThread = await WorkspaceBehaviour.LoadChatAsync(this.loadChat);
            
            if(this.ChatThread is not null)
            {
                this.MarkCurrentChatAsLoadedParameter();
                await this.ChatThreadChanged.InvokeAsync(this.ChatThread);
                this.Logger.LogInformation($"The chat '{this.ChatThread!.ChatId}' with title '{this.ChatThread.Name}' ({this.ChatThread.Blocks.Count} messages) was loaded successfully.");

                await this.SyncWorkspaceHeaderWithChatThreadAsync();
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

        var inputForbidden = this.IsInputForbidden();
        if (!inputForbidden && this.previousInputForbidden)
            await this.inputField.FocusAsync();

        this.previousInputForbidden = inputForbidden;
        await base.OnAfterRenderAsync(firstRender);
    }

    protected override async Task OnParametersSetAsync()
    {
        var incomingChatId = this.ChatThread?.ChatId ?? Guid.Empty;
        if (incomingChatId != this.lastSeenChatId || this.Provider != this.lastSeenProvider)
        {
            this.lastSeenChatId = incomingChatId;
            this.lastSeenProvider = this.Provider;
            this.previousInputForbidden = true;
        }

        await this.ApplyLoadedChatParameterAsync();
        await this.SyncForegroundChatAsync();
        await this.ConsumeMediaOutcomeAsync();
        await base.OnParametersSetAsync();
    }

    #endregion

    private async Task ApplyLoadedChatParameterAsync()
    {
        var chatId = this.ChatThread?.ChatId ?? Guid.Empty;
        var workspaceId = this.ChatThread?.WorkspaceId ?? Guid.Empty;

        if (this.loadedParameterChatId == chatId && this.loadedParameterWorkspaceId == workspaceId)
        {
            await this.SyncWorkspaceHeaderWithChatThreadAsync();
            return;
        }

        this.loadedParameterChatId = chatId;
        this.loadedParameterWorkspaceId = workspaceId;
        await this.LoadedChatChanged(notifyParent: false);
    }

    private void MarkCurrentChatAsLoadedParameter()
    {
        this.loadedParameterChatId = this.ChatThread?.ChatId ?? Guid.Empty;
        this.loadedParameterWorkspaceId = this.ChatThread?.WorkspaceId ?? Guid.Empty;
    }

    private async Task SyncWorkspaceHeaderWithChatThreadAsync()
    {
        var syncVersion = Interlocked.Increment(ref this.workspaceHeaderSyncVersion);
        var currentChatThread = this.ChatThread;
        if (currentChatThread is null)
        {
            this.ClearWorkspaceHeaderState();
            return;
        }

        // Guard: If ChatThread ID and WorkspaceId haven't changed, skip entirely.
        // Using ID-based comparison instead of name-based to correctly handle
        // temporary chats where the workspace name is always empty.
        if (this.currentChatThreadId == currentChatThread.ChatId
            && this.currentWorkspaceId == currentChatThread.WorkspaceId)
            return;

        var chatThreadId = currentChatThread.ChatId;
        var workspaceId = currentChatThread.WorkspaceId;
        var loadedWorkspaceName = await WorkspaceBehaviour.LoadWorkspaceNameAsync(workspaceId);

        // A newer sync request was started while awaiting IO. Ignore stale results.
        if (syncVersion != this.workspaceHeaderSyncVersion)
            return;

        // The active chat changed while loading the workspace name.
        if (this.ChatThread is null
            || this.ChatThread.ChatId != chatThreadId
            || this.ChatThread.WorkspaceId != workspaceId)
            return;

        this.currentChatThreadId = chatThreadId;
        this.currentWorkspaceId = workspaceId;
        this.PublishWorkspaceNameIfChanged(loadedWorkspaceName);
    }

    private void ClearWorkspaceHeaderState()
    {
        this.currentChatThreadId = Guid.Empty;
        this.currentWorkspaceId = Guid.Empty;
        this.PublishWorkspaceNameIfChanged(string.Empty);
    }

    private void PublishWorkspaceNameIfChanged(string workspaceName)
    {
        // Only notify the parent when the name actually changed to prevent
        // an infinite render loop: WorkspaceName -> UpdateWorkspaceName ->
        // StateHasChanged -> re-render -> OnParametersSetAsync -> WorkspaceName -> ...
        if (this.currentWorkspaceName == workspaceName)
            return;

        this.currentWorkspaceName = workspaceName;
        this.WorkspaceName(this.currentWorkspaceName);
    }

    private async Task RefreshRenamedWorkspaceHeaderAsync(Guid workspaceId)
    {
        var currentChatThread = this.ChatThread;
        if (currentChatThread is null || currentChatThread.WorkspaceId != workspaceId)
            return;

        var syncVersion = Interlocked.Increment(ref this.workspaceHeaderSyncVersion);
        var chatThreadId = currentChatThread.ChatId;
        var loadedWorkspaceName = await WorkspaceBehaviour.LoadWorkspaceNameAsync(workspaceId);

        if (syncVersion != this.workspaceHeaderSyncVersion)
            return;

        if (this.ChatThread is null
            || this.ChatThread.ChatId != chatThreadId
            || this.ChatThread.WorkspaceId != workspaceId)
            return;

        this.currentChatThreadId = chatThreadId;
        this.currentWorkspaceId = workspaceId;
        this.PublishWorkspaceNameIfChanged(loadedWorkspaceName);
    }

    private async Task SyncForegroundChatAsync()
    {
        var nextForegroundChatId = this.ChatThread?.ChatId ?? Guid.Empty;
        if (this.foregroundChatId == nextForegroundChatId)
            return;

        if (this.foregroundChatId != Guid.Empty)
            await this.AIJobService.SetForegroundAsync(AIJobKind.CHAT_GENERATION, this.foregroundChatId, false);

        this.foregroundChatId = nextForegroundChatId;
        if (this.foregroundChatId != Guid.Empty)
            await this.AIJobService.SetForegroundAsync(AIJobKind.CHAT_GENERATION, this.foregroundChatId, true);
    }

    private bool IsProviderSelected => this.Provider.UsedLLMProvider != LLMProviders.NONE;

    private bool IsCurrentChatStreaming => this.ChatThread is not null && this.AIJobService.IsChatGenerationActive(this.ChatThread.ChatId);
    
    private string ProviderPlaceholder => this.IsProviderSelected ? T("Type your input here...") : T("Select a provider first");

    private string InputLabel
    {
        get
        {
            if (this.IsProviderSelected)
                return string.Format(T("Your Prompt (use selected instance '{0}', provider '{1}')"), this.Provider.InstanceName, this.Provider.UsedLLMProvider.ToName());
            
            return this.T("Select a provider first");
        }
    }

    private bool CanThreadBeSaved => this.ChatThread is not null && this.ChatThread.Blocks.Any(b => !b.HideFromUser);
    
    private string TooltipAddChatToWorkspace => string.Format(T("Start new chat in workspace '{0}'"), this.currentWorkspaceName);

    private string UserInputStyle => this.SettingsManager.ConfigurationData.Confidence.ShowProviderConfidence ? this.Provider.UsedLLMProvider.GetConfidence(this.SettingsManager).SetColorStyle(this.SettingsManager) : string.Empty;

    private string UserInputClass => this.SettingsManager.ConfigurationData.Confidence.ShowProviderConfidence ? "confidence-border" : string.Empty;
    
    private void ApplyStandardDataSourceOptions()
    {
        var chatDefaultOptions = this.SettingsManager.ConfigurationData.Chat.PreselectedDataSourceOptions.CreateCopy();
        this.lastAppliedStandardDataSourceOptions = chatDefaultOptions.CreateCopy();
        this.earlyDataSourceOptions = chatDefaultOptions;
        if(this.ChatThread is not null)
            this.ChatThread.DataSourceOptions = chatDefaultOptions;
        
        this.dataSourceSelectionComponent?.ChangeOptionWithoutSaving(chatDefaultOptions);
    }

    private async Task ApplyUpdatedStandardDataSourceOptionsAfterConfigurationChange()
    {
        var updatedStandardOptions = this.SettingsManager.ConfigurationData.Chat.PreselectedDataSourceOptions.CreateCopy();
        var previousStandardOptions = this.lastAppliedStandardDataSourceOptions;
        this.lastAppliedStandardDataSourceOptions = updatedStandardOptions.CreateCopy();

        if (this.ChatThread is null)
        {
            this.earlyDataSourceOptions = updatedStandardOptions;
            this.dataSourceSelectionComponent?.ChangeOptionWithoutSaving(updatedStandardOptions);
            return;
        }

        if (!DataSourceOptionsAreEqual(this.ChatThread.DataSourceOptions, previousStandardOptions))
            return;

        await this.SetCurrentDataSourceOptions(updatedStandardOptions);
        this.dataSourceSelectionComponent?.ChangeOptionWithoutSaving(updatedStandardOptions, this.ChatThread.AISelectedDataSources);
        await this.ChatThreadChanged.InvokeAsync(this.ChatThread);
    }

    private static bool DataSourceOptionsAreEqual(DataSourceOptions left, DataSourceOptions right)
    {
        return left.DisableDataSources == right.DisableDataSources
               && left.AutomaticDataSourceSelection == right.AutomaticDataSourceSelection
               && left.AutomaticValidation == right.AutomaticValidation
               && left.PreselectedDataSourceIds.ToHashSet(StringComparer.Ordinal).SetEquals(right.PreselectedDataSourceIds);
    }
    
    private string ExtractThreadName(string firstUserInput)
    {
        // We select the first 10 words of the user input:
        var words = firstUserInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var threadName = string.Join(' ', words.Take(10));
        threadName = threadName.Trim();
        
        // Remove all line breaks:
        threadName = threadName.Replace("\r", string.Empty);
        threadName = threadName.Replace("\n", " ");
        threadName = threadName.Replace("\t", " ");
        
        // If the thread name is empty, we use a default name:
        if (string.IsNullOrWhiteSpace(threadName))
            threadName = "Thread";
        
        return threadName;
    }
    
    private async Task ProfileWasChanged(Profile profile)
    {
        this.currentProfile = this.SettingsManager.GetProfileById(profile.Id);
        if(this.ChatThread is null)
            return;

        this.ChatThread = this.ChatThread with
        {
            SelectedProfile = this.currentProfile.Id,
        };
        
        await this.ChatThreadChanged.InvokeAsync(this.ChatThread);
    }
    
    private async Task ChatTemplateWasChanged(ChatTemplate chatTemplate)
    {
        this.currentChatTemplate = this.SettingsManager.GetChatTemplateById(chatTemplate.Id);
        if(!string.IsNullOrWhiteSpace(this.currentChatTemplate.PredefinedUserPrompt))
            this.ComposerState.SetSystemInput(this.currentChatTemplate.PredefinedUserPrompt);

        // Apply template's file attachments (replaces existing):
        this.ComposerState.ReplaceFileAttachments(this.currentChatTemplate.FileAttachments);

        if(this.ChatThread is null)
            return;

        await this.StartNewChat(true);
    }

    private void RefreshCurrentProfileAndChatTemplate()
    {
        this.currentProfile = this.SettingsManager.GetProfileById(this.currentProfile.Id);
        this.currentChatTemplate = this.SettingsManager.GetChatTemplateById(this.currentChatTemplate.Id);
    }

    private async Task RefreshChatSelectionsAfterConfigurationChange()
    {
        var previousProvider = this.Provider;
        var previousChatTemplate = this.currentChatTemplate;
        var chatProviderId = this.ChatThread?.SelectedProvider;

        this.Provider = this.SettingsManager.GetChatProviderForLoadedChat(chatProviderId);
        if (this.Provider != previousProvider)
            await this.ProviderChanged.InvokeAsync(this.Provider);

        if (this.ChatThread is null)
        {
            this.currentProfile = this.SettingsManager.GetPreselectedProfile(Tools.Components.CHAT);
            this.currentChatTemplate = this.SettingsManager.GetPreselectedChatTemplate(Tools.Components.CHAT);
        }
        else
        {
            this.currentProfile = string.IsNullOrWhiteSpace(this.ChatThread.SelectedProfile)
                ? this.SettingsManager.GetProfileById(this.currentProfile.Id)
                : this.SettingsManager.GetProfileById(this.ChatThread.SelectedProfile);

            this.currentChatTemplate = string.IsNullOrWhiteSpace(this.ChatThread.SelectedChatTemplate)
                ? this.SettingsManager.GetChatTemplateById(this.currentChatTemplate.Id)
                : this.SettingsManager.GetChatTemplateById(this.ChatThread.SelectedChatTemplate);
        }

        if (!this.ComposerState.HasUserDraft && previousChatTemplate != this.currentChatTemplate)
            this.ComposerState.ApplyTemplate(this.currentChatTemplate);

        await this.ApplyUpdatedStandardDataSourceOptionsAfterConfigurationChange();
    }

    private IReadOnlyList<DataSourceAgentSelected> GetAgentSelectedDataSources()
    {
        if (this.ChatThread is null)
            return [];

        return this.ChatThread.AISelectedDataSources;
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

    private bool IsInputForbidden()
    {
        if (!this.IsProviderSelected)
            return true;
        
        if(this.IsCurrentChatStreaming)
            return true;
        
        if(!this.ChatThread.IsLLMProviderAllowed(this.Provider))
            return true;
        
        return false;
    }

    private async Task InputKeyEvent(KeyboardEventArgs keyEvent)
    {
        if(this.dataSourceSelectionComponent?.IsVisible ?? false)
            this.dataSourceSelectionComponent.Hide();
        
        this.hasUnsavedChanges = true;
        this.ComposerState.MarkUserDraft();
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

    private async Task ApplyMarkdownFormat(string formatType)
    {
        if (this.IsInputForbidden())
            return;

        if(this.dataSourceSelectionComponent?.IsVisible ?? false)
            this.dataSourceSelectionComponent.Hide();

        this.ComposerState.SetUserInput(await this.JsRuntime.InvokeAsync<string>("formatChatInputMarkdown", CHAT_INPUT_ID, formatType));
        this.hasUnsavedChanges = true;
    }

    private void ComposerAttachmentsChanged(HashSet<FileAttachment> attachments)
    {
        if (!ReferenceEquals(this.ComposerState.FileAttachments, attachments))
            this.ComposerState.ReplaceFileAttachments(attachments);

        this.ComposerState.MarkUserDraft();
        this.hasUnsavedChanges = true;
    }

    /// <summary>Creates and stores a stable draft immediately after media import confirmation.</summary>
    private async Task<ChatThread?> EnsureMediaImportChatAsync(string firstMediaPath)
    {
        if (this.ChatThread is not null)
            return this.ChatThread;

        this.RefreshCurrentProfileAndChatTemplate();
        var promptName = this.ExtractThreadName(this.ComposerState.UserInput);
        this.ChatThread = new()
        {
            IncludeDateTime = true,
            SelectedProvider = this.Provider.Id,
            SelectedProfile = this.currentProfile.Id,
            SelectedChatTemplate = this.currentChatTemplate.Id,
            SystemPrompt = SystemPrompts.DEFAULT,
            WorkspaceId = this.currentWorkspaceId,
            ChatId = Guid.NewGuid(),
            DataSourceOptions = this.earlyDataSourceOptions,
            Name = string.IsNullOrWhiteSpace(this.ComposerState.UserInput)
                ? $"Transkription: {Path.GetFileName(firstMediaPath)}"
                : promptName,
            Blocks = this.currentChatTemplate == ChatTemplate.NO_CHAT_TEMPLATE ? [] : this.currentChatTemplate.ExampleConversation.Select(block => block.DeepClone()).ToList(),
        };

        await WorkspaceBehaviour.StoreChatAsync(this.ChatThread);
        this.MarkCurrentChatAsLoadedParameter();
        await this.ChatThreadChanged.InvokeAsync(this.ChatThread);
        await this.SyncForegroundChatAsync();
        return this.ChatThread;
    }
    
    private async Task SendMessage(bool reuseLastUserPrompt = false)
    {
        if (this.MediaTranscriptionService.IsBusy(this.CurrentMediaImportOwner))
            return;

        if (!this.IsProviderSelected)
            return;
        
        if(!this.ChatThread.IsLLMProviderAllowed(this.Provider))
            return;

        this.RefreshCurrentProfileAndChatTemplate();

        // Blur the focus away from the input field to be able to clear it:
        await this.inputField.BlurAsync();
        
        // Create a new chat thread if necessary:
        if (this.ChatThread is null)
        {
            this.ChatThread = new()
            {
                IncludeDateTime = true,
                SelectedProvider = this.Provider.Id,
                SelectedProfile = this.currentProfile.Id,
                SelectedChatTemplate = this.currentChatTemplate.Id,
                SystemPrompt = SystemPrompts.DEFAULT,
                WorkspaceId = this.currentWorkspaceId,
                ChatId = Guid.NewGuid(),
                DataSourceOptions = this.earlyDataSourceOptions,
                Name = this.ExtractThreadName(this.ComposerState.UserInput),
                Blocks = this.currentChatTemplate == ChatTemplate.NO_CHAT_TEMPLATE ? [] : this.currentChatTemplate.ExampleConversation.Select(x => x.DeepClone()).ToList(),
            };
            
            this.MarkCurrentChatAsLoadedParameter();
            await this.ChatThreadChanged.InvokeAsync(this.ChatThread);
        }
        else
        {
            // Set the thread name if it is empty:
            if (string.IsNullOrWhiteSpace(this.ChatThread.Name))
                this.ChatThread.Name = this.ExtractThreadName(this.ComposerState.UserInput);
            
            // Update provider, profile and chat template:
            this.ChatThread.SelectedProvider = this.Provider.Id;
            this.ChatThread.SelectedProfile = this.currentProfile.Id;
            
            //
            // Remark: We do not update the chat template here
            // because the chat template is only used when starting a new chat.
            // Updating the chat template afterward is not supported.
            //
        }

        var time = DateTimeOffset.Now;
        IContent? lastUserPrompt;
        if (!reuseLastUserPrompt)
        {
            var normalizedAttachments = this.ComposerState.FileAttachments
                .Select(attachment => attachment.Normalize())
                .Where(attachment => attachment.IsValid)
                .ToList();

            lastUserPrompt = new ContentText
            {
                Text = this.ComposerState.UserInput,
                FileAttachments = normalizedAttachments,
            };
            this.ChatThread.PendingMediaTranscripts.Clear();

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
        await this.inputField.FocusAsync();

        this.ComposerState.Clear();

        await this.inputField.BlurAsync();
        
        // Enable the stream state for the chat component:
        this.hasUnsavedChanges = true;
        
        if (this.SettingsManager.ConfigurationData.Chat.ShowLatestMessageAfterLoading)
        {
            this.mustScrollToBottomAfterRender = true;
            this.scrollRenderCountdown = 2;
        }
        
        this.Logger.LogDebug($"Start processing user input using provider '{this.Provider.InstanceName}' with model '{this.Provider.Model}'.");
        await this.AIJobService.TryStartChatGenerationAsync(new ChatGenerationRequest
        {
            ChatThread = this.ChatThread!,
            AIText = aiText,
            LastUserPrompt = lastUserPrompt,
            ProviderSettings = this.Provider,
            IsForeground = true,
        });

        await this.SyncForegroundChatAsync();
        this.StateHasChanged();
    }
    
    private async Task CancelStreaming()
    {
        if (this.ChatThread is not null)
            await this.AIJobService.CancelChatGenerationAsync(this.ChatThread.ChatId);
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
            await this.Workspaces.StoreChatAsync(this.ChatThread);
        else
            await WorkspaceBehaviour.StoreChatAsync(this.ChatThread);
        
        this.hasUnsavedChanges = false;
    }
    
    private async Task StartNewChat(bool useSameWorkspace = false, bool deletePreviousChat = false)
    {
        //
        // Want the user to manage the chat storage manually? In that case, we have to ask the user
        // about possible data loss:
        //
        if (this.SettingsManager.ConfigurationData.Workspace.StorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_MANUALLY && this.hasUnsavedChanges && !this.IsCurrentChatStreaming)
        {
            var dialogParameters = new DialogParameters<ConfirmDialog>
            {
                { x => x.Message, "Are you sure you want to start a new chat? All unsaved changes will be lost." },
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
                await WorkspaceBehaviour.DeleteChatAsync(this.DialogService, this.ChatThread.WorkspaceId, this.ChatThread.ChatId, askForConfirmation: false);
            else
                await this.Workspaces.DeleteChatAsync(chatPath, askForConfirmation: false, unloadChat: true);
        }

        //
        // Reset our state:
        //
        this.hasUnsavedChanges = false;
        this.ComposerState.Clear();
        this.RefreshCurrentProfileAndChatTemplate();
        
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
                if(this.Provider == AIStudio.Settings.Provider.NONE)
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
            this.ClearWorkspaceHeaderState();
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
                IncludeDateTime = true,
                SelectedProvider = this.Provider.Id,
                SelectedProfile = this.currentProfile.Id,
                SelectedChatTemplate = this.currentChatTemplate.Id,
                SystemPrompt = SystemPrompts.DEFAULT,
                WorkspaceId = this.currentWorkspaceId,
                ChatId = Guid.NewGuid(),
                Name = string.Empty,
                Blocks = this.currentChatTemplate == ChatTemplate.NO_CHAT_TEMPLATE ? [] : this.currentChatTemplate.ExampleConversation.Select(x => x.DeepClone()).ToList(),
            };
        }

        this.ComposerState.ApplyTemplate(this.currentChatTemplate);

        // Now, we have to reset the data source options as well:
        this.ApplyStandardDataSourceOptions();
        
        // Notify the parent component about the change:
        await this.SyncForegroundChatAsync();
        this.MarkCurrentChatAsLoadedParameter();
        await this.ChatThreadChanged.InvokeAsync(this.ChatThread);
    }
    
    private async Task MoveChatToWorkspace()
    {
        if(this.ChatThread is null)
            return;
        
        if (this.SettingsManager.ConfigurationData.Workspace.StorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_MANUALLY && this.hasUnsavedChanges && !this.IsCurrentChatStreaming)
        {
            var confirmationDialogParameters = new DialogParameters<ConfirmDialog>
            {
                { x => x.Message, T("Are you sure you want to move this chat? All unsaved changes will be lost.") },
            };
        
            var confirmationDialogReference = await this.DialogService.ShowAsync<ConfirmDialog>("Unsaved Changes", confirmationDialogParameters, DialogOptions.FULLSCREEN);
            var confirmationDialogResult = await confirmationDialogReference.Result;
            if (confirmationDialogResult is null || confirmationDialogResult.Canceled)
                return;
        }
        
        var dialogParameters = new DialogParameters<WorkspaceSelectionDialog>
        {
            { x => x.Message, T("Please select the workspace where you want to move the chat to.") },
            { x => x.SelectedWorkspace, this.ChatThread?.WorkspaceId ?? Guid.Empty },
            { x => x.ConfirmText, T("Move chat") },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<WorkspaceSelectionDialog>(T("Move Chat to Workspace"), dialogParameters, DialogOptions.FULLSCREEN_MANUAL_ESCAPE);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var workspaceId = dialogResult.Data is Guid id ? id : Guid.Empty;
        if (workspaceId == Guid.Empty)
            return;
        
        await WorkspaceBehaviour.MoveChatAsync(this.ChatThread!, workspaceId);
        this.MarkCurrentChatAsLoadedParameter();

        await this.SyncWorkspaceHeaderWithChatThreadAsync();
    }
    
    private async Task LoadedChatChanged(bool notifyParent = true)
    {
        this.hasUnsavedChanges = false;
        this.ComposerState.Clear();

        if (this.ChatThread is not null)
        {
            this.ChatThread = this.AIJobService.TryGetLiveChatThread(this.ChatThread.ChatId) ?? this.ChatThread;
            this.loadedParameterChatId = this.ChatThread.ChatId;
            this.loadedParameterWorkspaceId = this.ChatThread.WorkspaceId;
            if (notifyParent)
                await this.ChatThreadChanged.InvokeAsync(this.ChatThread);

            await this.SyncWorkspaceHeaderWithChatThreadAsync();
            await this.SyncForegroundChatAsync();
            this.dataSourceSelectionComponent?.ChangeOptionWithoutSaving(this.ChatThread.DataSourceOptions, this.ChatThread.AISelectedDataSources);
        }
        else
        {
            this.loadedParameterChatId = Guid.Empty;
            this.loadedParameterWorkspaceId = Guid.Empty;
            this.ClearWorkspaceHeaderState();
            await this.SyncForegroundChatAsync();
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
        this.hasUnsavedChanges = false;
        this.ComposerState.Clear();
        this.ClearWorkspaceHeaderState();
        
        this.ChatThread = null;
        this.MarkCurrentChatAsLoadedParameter();
        await this.SyncForegroundChatAsync();
        this.ApplyStandardDataSourceOptions();
        await this.ChatThreadChanged.InvokeAsync(this.ChatThread);
    }
    
    private async Task SelectProviderWhenLoadingChat()
    {
        var chatProvider = this.ChatThread?.SelectedProvider;
        var chatProfile = this.ChatThread?.SelectedProfile;
        var chatChatTemplate = this.ChatThread?.SelectedChatTemplate;

        this.Provider = this.SettingsManager.GetChatProviderForLoadedChat(chatProvider);
        
        await this.ProviderChanged.InvokeAsync(this.Provider);

        // Try to select the profile:
        if (!string.IsNullOrWhiteSpace(chatProfile))
            this.currentProfile = this.SettingsManager.GetProfileById(chatProfile);
        
        // Try to select the chat template:
        if (!string.IsNullOrWhiteSpace(chatChatTemplate))
            this.currentChatTemplate = this.SettingsManager.GetChatTemplateById(chatChatTemplate);
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
        
        if(!this.ChatThread.IsLLMProviderAllowed(this.Provider))
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
        
        this.RestoreComposerFromTextBlock(textBlock);
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
        
        this.RestoreComposerFromTextBlock(textBlock);
        this.ChatThread.Remove(block);
        this.hasUnsavedChanges = true;
        this.StateHasChanged();
        
        return Task.CompletedTask;
    }

    private void RestoreComposerFromTextBlock(ContentText textBlock)
    {
        this.ComposerState.RestoreFromTextBlock(textBlock);
    }
    
    #region Overrides of MSGComponentBase
    
    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.RESET_CHAT_STATE:
                await this.ResetState();
                break;
            
            case Event.CHAT_STREAMING_DONE:
                // Streaming mutates the last AI block over time.
                // In manual storage mode, a save during streaming must not
                // mark the final streamed state as already persisted.
                this.hasUnsavedChanges = true;
                if(this.autoSaveEnabled)
                    await this.SaveThread();
                break;

            case Event.WORKSPACE_RENAMED:
                if (data is Guid workspaceId)
                    await this.RefreshRenamedWorkspaceHeaderAsync(workspaceId);
                break;

            case Event.CONFIGURATION_CHANGED:
            case Event.PLUGINS_RELOADED:
                await this.RefreshChatSelectionsAfterConfigurationChange();
                this.StateHasChanged();
                break;
            
            case Event.AI_JOB_CHANGED:
            case Event.AI_JOB_FINISHED:
            case Event.CHAT_GENERATION_CHANGED:
                if (data is AIJobSnapshot { Kind: AIJobKind.CHAT_GENERATION } snapshot && this.ChatThread?.ChatId == snapshot.SubjectId)
                {
                    this.ChatThread = this.AIJobService.TryGetLiveChatThread(snapshot.SubjectId) ?? this.ChatThread;
                    if (!snapshot.IsActive)
                    {
                        this.hasUnsavedChanges = false;
                        this.previousInputForbidden = true;
                    }

                    this.StateHasChanged();
                }
                break;
        }
    }

    protected override Task<TResult?> ProcessIncomingMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data) where TResult : default where TPayload : default
    {
        switch (triggeredEvent)
        {
            case Event.HAS_CHAT_UNSAVED_CHANGES:
                if(this.SettingsManager.ConfigurationData.Workspace.StorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY)
                    return Task.FromResult((TResult?) (object) false);

                if (this.IsCurrentChatStreaming)
                    return Task.FromResult((TResult?) (object) false);
                
                return Task.FromResult((TResult?)(object)(this.hasUnsavedChanges || this.ComposerState.HasVisibleUserDraft));
        }
        
        return Task.FromResult(default(TResult));
    }

    #endregion
    
    #region Implementation of IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        this.MediaTranscriptionService.StateChanged -= this.OnMediaImportStateChanged;
        if(this.SettingsManager.ConfigurationData.Workspace.StorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY)
        {
            await this.SaveThread();
            this.hasUnsavedChanges = false;
        }

        await this.AIJobService.SetForegroundAsync(AIJobKind.CHAT_GENERATION, this.foregroundChatId, false);
        this.Dispose();
    }

    #endregion
}