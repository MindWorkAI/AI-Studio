using System.Globalization;

using AIStudio.Chat;
using AIStudio.Dialogs;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.AIJobs;
using AIStudio.Tools.Media;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

public partial class ChatComponent : MSGComponentBase
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

    /// <summary>
    /// Tells whether an answer is being written for the open chat, whenever that changes.
    /// </summary>
    /// <remarks>
    /// For the parts of the chat page outside this component, such as the provider selection. The AI
    /// job reports every part of an answer, and this component renders after each report. The page
    /// learns only when an answer starts or ends, see OnAfterRenderAsync.
    /// </remarks>
    [Parameter]
    public EventCallback<bool> IsAnswerBeingWrittenChanged { get; set; }
    
    [Parameter]
    public Action<string> WorkspaceName { get; set; } = _ => { };
    
    [Parameter]
    public Workspaces? Workspaces { get; set; }

    [Parameter]
    public ChatComposerState ComposerState { get; set; } = new();
    
    [Inject]
    private ILogger<ChatComponent> Logger { get; set; } = null!;

    [Inject]
    private ToolRegistry ToolRegistry { get; set; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private ConversationTokenCounter ConversationTokenCounter { get; init; } = null!;
    
    [Inject]
    private IJSRuntime JsRuntime { get; init; } = null!;

    [Inject]
    private AIJobService AIJobService { get; init; } = null!;

    [Inject]
    private MediaTranscriptionService MediaTranscriptionService { get; init; } = null!;

    [Inject]
    private NavigationManager NavigationManager { get; init; } = null!;

    private const Placement TOOLBAR_TOOLTIP_PLACEMENT = Placement.Top;
    private static readonly Dictionary<string, object?> USER_INPUT_ATTRIBUTES = new();

    private DataSourceSelection? dataSourceSelectionComponent;
    private DataSourceOptions earlyDataSourceOptions = new();
    private DataSourceOptions lastAppliedAutomaticDataSourceOptions = new();
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
    private HashSet<string> selectedToolIds = [];
    private bool previousInputForbidden = true;
    private bool announcedAnswerBeingWritten;
    private Guid lastSeenChatId = Guid.Empty;
    private AIStudio.Settings.Provider lastSeenProvider = AIStudio.Settings.Provider.NONE;
    private string currentWorkspaceName = string.Empty;
    private Guid currentWorkspaceId = Guid.Empty;
    private Guid currentChatThreadId = Guid.Empty;
    private Guid loadedParameterChatId = Guid.Empty;
    private Guid loadedParameterWorkspaceId = Guid.Empty;
    private bool hasAppliedChatParameter;
    private Guid foregroundChatId = Guid.Empty;
    private int workspaceHeaderSyncVersion;
    private ConversationTokens conversationTokens = ConversationTokens.UNAVAILABLE;

    /// <summary>
    /// How much of the window must be used before the number starts saying so.
    /// </summary>
    private const double WINDOW_NEARLY_FULL = 0.8d;

    /// <summary>
    /// How long the token count stays quiet after it ran, while nothing is being written.
    /// </summary>
    /// <remarks>
    /// A render is cheap to ask about and a count is not. This is what keeps a burst of renders --
    /// loading a chat touches several things in a row -- from turning into a burst of counting,
    /// while staying short enough that switching a profile moves the number right away.
    /// </remarks>
    private static readonly TimeSpan TOKEN_COUNT_QUIET_TIME = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// How long the token count stays quiet while an answer is being written.
    /// </summary>
    /// <remarks>
    /// An answer grows with every word, so each count finds a new number, shows it, and thereby
    /// renders -- which asks for the next count. That makes this the whole cadence while a model
    /// writes, and three seconds is the pace the chat itself keeps: the job service hands its
    /// progress to the screen no more often than that.
    /// </remarks>
    private static readonly TimeSpan TOKEN_COUNT_STREAMING_QUIET_TIME = TimeSpan.FromSeconds(3);

    /// <summary>
    /// How long the token count waits for a reason before counting anyway.
    /// </summary>
    /// <remarks>
    /// For what happens outside AI Studio: an attached document is read from disk every time it is
    /// sent, so somebody editing it in another program changes what the next message costs without
    /// anything here rendering.
    /// </remarks>
    private static readonly TimeSpan TOKEN_COUNT_HEARTBEAT = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Recomputes the token count whenever something might have changed.
    /// </summary>
    private ConversationTokenTracker? tokenTracker;

    /// <summary>
    /// How long the user has to pause before the open chat is stored with the message being written.
    /// </summary>
    private static readonly TimeSpan DRAFT_AUTOSAVE_DELAY = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Stores the open chat with the message being written, while chats are stored automatically.
    /// </summary>
    private ChatDraftAutosave? draftAutosave;

    /// <summary>
    /// The only way to change the messages of the open chat, which is locked while an answer is being written.
    /// </summary>
    private ChatBlockActions blockActions = null!;

    /// <summary>
    /// A new chat which has not reached the disk yet, or null.
    /// </summary>
    /// <remarks>
    /// Starting a new chat in a workspace creates its thread right away, so that it keeps the
    /// workspace, but stores it only together with its first message. Until then, leaving the chat
    /// stores it when the user started a message in it, see PreserveNewChatDraftAsync. Which chat
    /// that is has to be noted here: once the chat is left, the component may show the next one
    /// already. Nor does the disk tell, since a deleted chat is missing there as well, and storing
    /// that one would bring it back. Hence, this is always the chat on the screen, and it is
    /// forgotten as soon as the chat was stored or left.
    /// </remarks>
    private ChatThread? unstoredChat;

    /// <summary>
    /// Lets this component store a new chat before the user leaves the chat page.
    /// </summary>
    private IDisposable? locationChangingRegistration;

    /// <summary>
    /// How long to leave the token count alone after it ran.
    /// </summary>
    private TimeSpan TokenCountQuietTime() => this.IsCurrentChatStreaming ? TOKEN_COUNT_STREAMING_QUIET_TIME : TOKEN_COUNT_QUIET_TIME;

    /// <summary>
    /// The culture the token numbers are written in.
    /// </summary>
    /// <remarks>
    /// Taken from the language plugin the user chose, not from the machine. AI Studio's language is
    /// a setting of its own, and a German who set German would otherwise read English separators
    /// inside a German sentence -- where "1,234" means something a thousand times smaller.
    /// </remarks>
    private CultureInfo currentCulture = CultureInfo.InvariantCulture;

    /// <summary>
    /// What the helper text under the input field says about the token budget.
    /// </summary>
    /// <remarks>
    /// The number of the conversation and the window stand in four whole sentences, because a
    /// translator needs to see the whole thing: which of the two numbers is the limit, and where the
    /// word for "about" belongs, are decisions no language makes the same way. What follows -- the
    /// tools' share, the draft, the pictures -- is added as clauses which are whole phrases in turn,
    /// each with the one number it is about.
    ///
    /// The images are named rather than counted. Every vendor charges a picture differently, and
    /// none of those rules can be applied without decoding the file, so the honest answer is to say
    /// how many of them the number does not include.
    /// </remarks>
    private string TokenCountMessage
    {
        get
        {
            if (!this.conversationTokens.IsKnown)
                return string.Empty;

            //
            // Several statements, and which of them can be trusted differs. What the conversation
            // has cost is exact wherever the provider said it; the window is whatever somebody
            // wrote down about the model; what the tools add is only there while they work;
            // what is being written has never been sent and can only ever be estimated. So they
            // are named apart, and the word which marks a guess sits where the guess is.
            //
            var history = TokenAmount.Format(this.conversationTokens.HistoryTokens, this.currentCulture);
            var historyIsExact = this.conversationTokens.HistoryIsReported || !this.conversationTokens.IsEstimate;
            var budget = this.conversationTokens.Window.IsKnown
                ? string.Format(historyIsExact ? this.T("{0} of {1} tokens") : this.T("approx. {0} of {1} tokens"), history, TokenAmount.Format(this.conversationTokens.Window.DefaultTokens, this.currentCulture))
                : string.Format(historyIsExact ? this.T("{0} tokens") : this.T("approx. {0} tokens"), history);

            //
            // Said while the tools work, so that the number does not climb and fall back without a
            // reason anybody could see: all of it is sent with every round of this request, and none
            // of it with the next message.
            //
            if (this.conversationTokens.ToolTokens > 0)
                budget = string.Format(this.T("{0}, of which approx. {1} from tools"), budget, TokenAmount.Format(this.conversationTokens.ToolTokens, this.currentCulture));

            if (this.conversationTokens.DraftTokens > 0)
                budget = string.Format(this.T("{0}, plus approx. {1} for your message"), budget, TokenAmount.Format(this.conversationTokens.DraftTokens, this.currentCulture));

            //
            // The pictures of the whole conversation, not of the message being written: every one
            // of them is sent again with every further message, so a chat runs past the model's
            // limit long after anybody last thought about images. That is worth saying even when the
            // provider counted all of them.
            //
            if (this.conversationTokens.TooManyImages)
                return $"{budget} {string.Format(this.T("plus {0} image(s), which is more than the {1} this model accepts"), this.conversationTokens.Images, this.conversationTokens.ImageLimits.MaxInOneMessage)}";

            if (this.conversationTokens.UncountedImages is 0)
                return budget;

            return $"{budget} {string.Format(this.T("plus {0} image(s), which cannot be counted"), this.conversationTokens.UncountedImages)}";
        }
    }

    /// <summary>
    /// Takes over the culture of the language the user chose for AI Studio.
    /// </summary>
    private async Task RefreshCulture()
    {
        var activeLanguagePlugin = await this.SettingsManager.GetActiveLanguagePlugin();
        this.currentCulture = CommonTools.DeriveActiveCultureOrInvariant(activeLanguagePlugin.IETFTag);
    }

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
    /// to track whether the user has drafted any input, and hands the input to
    /// the draft of the open chat.
    /// </remarks>
    private string UserInput
    {
        get => this.ComposerState.UserInput;
        set
        {
            this.ComposerState.SetUserInput(value);
            this.KeepComposerInDraft();

            // Also for text which arrives without a key, e.g., pasted with the mouse:
            this.hasUnsavedChanges = true;
        }
    }

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.MediaTranscriptionService.StateChanged += this.OnMediaImportStateChanged;

        // Created before anything is awaited, because the first render may happen there already:
        this.blockActions = new(chatId => this.AIJobService.IsChatGenerationActive(chatId));

        //
        // Leaving the chat page takes the composer along. Registered before anything is awaited, so
        // that a component which goes away while it starts can never leave its handler behind:
        //
        this.locationChangingRegistration = this.NavigationManager.RegisterLocationChangingHandler(this.OnLocationChangingAsync);
        await this.RefreshCulture();

        //
        // The number under the input field follows from the conversation, and nothing in a
        // conversation announces that it changed: blocks, attachments and the answer being written
        // are plain objects somebody mutates. So it is recomputed rather than notified.
        //
        this.tokenTracker = new(this.RecountTokensAsync, this.TokenCountQuietTime, TOKEN_COUNT_HEARTBEAT);
        this.tokenTracker.Start();

        this.draftAutosave = new(
            DRAFT_AUTOSAVE_DELAY,
            chat => WorkspaceBehaviour.IsChatExisting(new LoadChat(chat.WorkspaceId, chat.ChatId)),
            chat => this.AIJobService.IsChatGenerationActive(chat.ChatId),
            WorkspaceBehaviour.StoreChatAsync,
            this.Logger);

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

        await this.ApplyChatTemplateToolSelectionAsync();
        this.lastAppliedAutomaticDataSourceOptions = this.GetAutomaticDataSourceOptions();

        var deferredInput = MessageBus.INSTANCE.TakeDeferredMessages<string>(Event.SEND_TO_CHAT_INPUT).LastOrDefault();
        if (!string.IsNullOrWhiteSpace(deferredInput))
            this.ComposerState.SetUserInput(deferredInput);

        //
        // Check for deferred messages of the kind 'SEND_TO_CHAT',
        // aka the user sends an assistant result to the chat:
        //
        var deferredRequest = MessageBus.INSTANCE.TakeDeferredMessages<ChatStartRequest>(Event.SEND_TO_CHAT).LastOrDefault();
        if (deferredRequest is not null)
        {
            //
            // Yes, the user sent an assistant result to the chat.
            //
            
            //
            // Use chat thread sent by the user, with a draft of its own. The thread is a copy of
            // the assistant's one, made with a with expression, which shares the draft. An assistant
            // has no composer, so its draft is always empty, but the next chat it starts would
            // otherwise begin with whatever the user writes in this one.
            //
            this.ChatThread = deferredRequest.ChatThread with { Draft = new() };
            this.ChatThread.IncludeDateTime = true;
            this.ApplyToolSelectionOfLoadedChat();

            //
            // Apply the chat template of the incoming chat to the composer. Like everywhere else,
            // a draft the user typed themselves wins: we must not discard it just because someone
            // started a preconfigured chat in the meantime.
            //
            if (deferredRequest.ApplySelectedChatTemplateToComposer && !this.ComposerState.HasUserDraft)
                this.ComposerState.ApplyTemplate(this.SettingsManager.GetChatTemplateById(this.ChatThread.SelectedChatTemplate));

            // That draft is the message of the incoming chat now:
            if (this.ComposerState.HasUserDraft)
                this.MirrorComposerIntoDraft(this.ChatThread);

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
                if (!deferredRequest.PreserveDataSourceOptions &&
                    this.SettingsManager.ConfigurationData.Chat.SendToChatDataSourceBehavior is SendToChatDataSourceBehavior.APPLY_STANDARD_CHAT_DATA_SOURCE_OPTIONS)
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
            this.ApplyAutomaticDataSourceOptions();
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
        var deferredLoading = MessageBus.INSTANCE.TakeDeferredMessages<LoadChat>(Event.LOAD_CHAT).LastOrDefault();
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
            this.InvokeAsync(async () =>
            {
                await this.ConsumeMediaOutcomeAsync();
                this.StateHasChanged();
            }).Observe($"{nameof(ChatComponent)}: consuming a media import outcome");
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
            await this.StoreAndListChatAsync(this.ChatThread);
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

                this.ApplyToolSelectionOfLoadedChat();
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

        //
        // The page hears about an answer starting or ending here rather than with every report of the
        // AI job, since a render of the page redraws the workspace tree. This component renders after
        // every such report and after another chat was opened, so the page never misses a change. The
        // first render reports in any case: the page may still hold what another instance of this
        // component told it, e.g., before the workspaces were shown or hidden.
        //
        var isAnswerBeingWritten = this.IsCurrentChatStreaming;
        if (firstRender || isAnswerBeingWritten != this.announcedAnswerBeingWritten)
        {
            this.announcedAnswerBeingWritten = isAnswerBeingWritten;
            await this.IsAnswerBeingWrittenChanged.InvokeAsync(isAnswerBeingWritten);
        }

        //
        // Everything which can move the token count also renders this component: the selections in
        // the toolbar, the attachments and the composer all travel through an event callback whose
        // receiver is this component, and the streamed answer arrives as a message which already
        // asks for a render. So this one line stands in for the fifteen call sites which used to be
        // spread over this file -- and which kept missing one.
        //
        this.tokenTracker?.Nudge();
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

        //
        // The first chat of this component is no chat being left. The component appears anew, e.g.,
        // whenever the workspace sidebar is shown or hidden, while the page keeps the chat and the
        // composer:
        //
        var isFirstChat = !this.hasAppliedChatParameter;
        this.hasAppliedChatParameter = true;

        if (this.loadedParameterChatId == chatId && this.loadedParameterWorkspaceId == workspaceId)
        {
            await this.SyncWorkspaceHeaderWithChatThreadAsync();
            return;
        }

        //
        // The same chat in another workspace was moved there, e.g., through the workspace tree. Only
        // a move changes the workspace of a chat; every other way to open one brings another chat ID:
        //
        if (chatId != Guid.Empty && this.loadedParameterChatId == chatId)
        {
            await this.FollowMovedChatAsync(this.loadedParameterWorkspaceId);
            return;
        }

        var leftChatHadNoThread = !isFirstChat && this.loadedParameterChatId == Guid.Empty;
        this.loadedParameterChatId = chatId;
        this.loadedParameterWorkspaceId = workspaceId;

        // The chat being left keeps the message the user started in it:
        await this.FlushDraftAsync();
        await this.PreserveNewChatDraftAsync(leftChatHadNoThread);
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

    private bool CanThreadBeCopied => this.CanThreadBeSaved && !this.IsCurrentChatStreaming && !this.MediaTranscriptionService.IsBusy(this.CurrentMediaImportOwner);

    /// <summary>
    /// Whether a chat template can be made from the open chat. While an answer is being written, the template would copy it unfinished.
    /// </summary>
    private bool CanThreadBecomeTemplate => this.CanThreadBeSaved && !this.IsCurrentChatStreaming;

    /// <summary>
    /// Whether the user may pick another profile, which the open chat carries from then on.
    /// </summary>
    /// <remarks>
    /// Picking a profile replaces the thread with a copy which carries it. While an answer is being
    /// written, the next update of the AI job puts the thread of the job back in place of that copy,
    /// and the choice is lost.
    /// </remarks>
    private bool CanProfileBeChanged => this.currentChatTemplate.AllowProfileUsage && !this.IsCurrentChatStreaming;

    /// <summary>
    /// Whether the user may change the attachments of the message being written.
    /// </summary>
    /// <remarks>
    /// Not while an answer is being written, just like the text of the message, and not while the
    /// audio or video files of the chat are being transcribed.
    /// </remarks>
    private bool CanAttachFiles => !this.IsCurrentChatStreaming && !this.MediaTranscriptionService.IsBusy(this.CurrentMediaImportOwner);

    // The chat template locks the profile for good, an answer being written only until it is complete:
    private string ProfileDisabledText => this.currentChatTemplate.AllowProfileUsage
        ? T("Profile changes are locked while a response is running.")
        : T("Profile usage is disabled according to your chat template settings.");

    /// <summary>
    /// Whether the open chat can move into a workspace, which is exactly when the workspace tree offers it.
    /// </summary>
    /// <remarks>
    /// The tree lists every stored chat, even one without a message yet, such as a chat which was
    /// stored right away to keep the transcript of an audio or video file. A chat not stored yet has
    /// no directory to move. While an answer is generated or a transcript is made, the chat stays
    /// where it is, just like in the tree. The directory check comes last, because this runs with
    /// every render.
    /// </remarks>
    private bool CanThreadBeMoved => this.ChatThread is not null
                                     && !this.IsCurrentChatStreaming
                                     && !this.MediaTranscriptionService.IsBusy(this.CurrentMediaImportOwner)
                                     && WorkspaceBehaviour.IsChatExisting(new LoadChat(this.ChatThread.WorkspaceId, this.ChatThread.ChatId));

    private string TooltipAddChatToWorkspace => string.Format(T("Start new chat in workspace '{0}'"), this.currentWorkspaceName);

    private string UserInputStyle => this.SettingsManager.ConfigurationData.Confidence.ShowProviderConfidence ? this.Provider.UsedLLMProvider.GetConfidence(this.SettingsManager).SetColorStyle(this.SettingsManager) : string.Empty;

    private string UserInputClass => $"{(this.SettingsManager.ConfigurationData.Confidence.ShowProviderConfidence ? "confidence-border" : string.Empty)} {this.TokenBudgetClass}".Trim();

    /// <summary>
    /// How much of the model's context window the conversation already takes.
    /// </summary>
    /// <remarks>
    /// Zero whenever nobody wrote the window down. There is then nothing to be full of, and a share
    /// of an unknown total would be a number made up on the spot.
    /// </remarks>
    private double TokenBudgetFill => this.conversationTokens is { IsKnown: true, Window.IsKnown: true }
        ? (double) this.conversationTokens.Tokens / this.conversationTokens.Window.DefaultTokens
        : 0d;

    /// <summary>
    /// What the number under the input field is coloured with, if anything.
    /// </summary>
    /// <remarks>
    /// Two steps rather than a gradient: below four fifths there is nothing to do about it, above
    /// it there is -- shorten the chat, start a new one, or pick a model which reads more -- and
    /// past the window the request will be refused or trimmed by the provider.
    ///
    /// Images share the second step and have no first one. There is no "nearly too many pictures":
    /// either they fit or the request comes back as an error, and no number of them is worth a
    /// warning as long as it fits.
    /// </remarks>
    private string TokenBudgetClass
    {
        get
        {
            //
            // Too many pictures is the same kind of news as a full window: the request will be
            // refused, and for the same reason -- more was put in than the model takes.
            //
            if (this.conversationTokens.TooManyImages || this.TokenBudgetFill >= 1d)
                return "token-budget-exceeded";

            return this.TokenBudgetFill >= WINDOW_NEARLY_FULL ? "token-budget-nearly-full" : string.Empty;
        }
    }
    
    /// <summary>
    /// Picks the tools a chat starts with: those of its chat template, or the chat defaults.
    /// </summary>
    /// <remarks>
    /// A preselection, not a limit — the user changes it in the chat as usual. A template without a
    /// tool selection says nothing about tools and therefore leaves the chat default in place,
    /// which is a different statement from a template that selects no tool at all.
    /// </remarks>
    private async Task ApplyChatTemplateToolSelectionAsync()
    {
        if (this.currentChatTemplate.ToolIds is not { } templateToolIds)
        {
            this.selectedToolIds = this.ToolRegistry.GetDefaultToolIds(Tools.Components.CHAT);
            return;
        }

        //
        // Only the tools the user could have switched on themselves: a template may name one whose
        // settings are incomplete — an unconfigured web search, say — and starting with it enabled
        // would show a state the user cannot produce by hand and cannot fix from the chat.
        //
        this.selectedToolIds = await this.ToolRegistry.FilterSelectableToolIdsAsync(Tools.Components.CHAT, templateToolIds);
    }

    /// <summary>
    /// Takes over the profile and provider which the chat template preselects, wherever it preselects one.
    /// </summary>
    /// <remarks>
    /// A preselection, not a limit — the user changes both in the chat as usual. A template which
    /// says nothing, names something which is gone, or names a provider which may not be used for
    /// chats leaves the current choice in place, so the chat starts as it would without the
    /// template. The caller tells the parent about a new provider.
    /// </remarks>
    /// <returns>True when the provider changed.</returns>
    private bool ApplyChatTemplateProfileAndProvider()
    {
        if (this.SettingsManager.GetChatTemplateProfile(this.currentChatTemplate) is { } templateProfile)
            this.currentProfile = templateProfile;

        var templateProvider = this.SettingsManager.GetChatTemplateProvider(this.currentChatTemplate);
        if (templateProvider == AIStudio.Settings.Provider.NONE || templateProvider == this.Provider)
            return false;

        this.Provider = templateProvider;
        return true;
    }

    /// <summary>
    /// The data source options a chat starts with: those of its chat template, or the chat defaults.
    /// </summary>
    /// <remarks>
    /// As with the tools, a template which carries no options says nothing and leaves the chat
    /// defaults in place. A template which carries them answers more than which sources to search:
    /// whether data sources are used at all, and whether an agent picks them for each message.
    /// </remarks>
    private DataSourceOptions GetAutomaticDataSourceOptions() =>
        this.currentChatTemplate.DataSourceOptions?.CreateCopy() ?? this.SettingsManager.ConfigurationData.Chat.PreselectedDataSourceOptions.CreateCopy();

    private void ApplyAutomaticDataSourceOptions()
    {
        var automaticOptions = this.GetAutomaticDataSourceOptions();
        this.lastAppliedAutomaticDataSourceOptions = automaticOptions.CreateCopy();
        this.earlyDataSourceOptions = automaticOptions;
        if(this.ChatThread is not null)
            this.ChatThread.DataSourceOptions = automaticOptions;

        this.dataSourceSelectionComponent?.ChangeOptionWithoutSaving(automaticOptions);
    }

    private async Task ApplyUpdatedAutomaticDataSourceOptionsAfterConfigurationChange()
    {
        //
        // What a chat would start with right now. The chat template is asked first, so that editing
        // the template of the current chat reaches it — a change of the chat defaults it does not
        // use would say nothing about it.
        //
        var updatedAutomaticOptions = this.GetAutomaticDataSourceOptions();
        var previousAutomaticOptions = this.lastAppliedAutomaticDataSourceOptions;
        this.lastAppliedAutomaticDataSourceOptions = updatedAutomaticOptions.CreateCopy();

        if (this.ChatThread is null)
        {
            this.earlyDataSourceOptions = updatedAutomaticOptions;
            this.dataSourceSelectionComponent?.ChangeOptionWithoutSaving(updatedAutomaticOptions);
            return;
        }

        if (!DataSourceOptionsAreEqual(this.ChatThread.DataSourceOptions, previousAutomaticOptions))
            return;

        await this.SetCurrentDataSourceOptions(updatedAutomaticOptions);
        this.dataSourceSelectionComponent?.ChangeOptionWithoutSaving(updatedAutomaticOptions, this.ChatThread.AISelectedDataSources);
        await this.ChatThreadChanged.InvokeAsync(this.ChatThread);
    }

    private static bool DataSourceOptionsAreEqual(DataSourceOptions left, DataSourceOptions right)
    {
        return left.DisableDataSources == right.DisableDataSources
               && left.AutomaticDataSourceSelection == right.AutomaticDataSourceSelection
               && left.AutomaticValidation == right.AutomaticValidation
               && left.RetrievalMode == right.RetrievalMode
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
        if (!this.CanProfileBeChanged)
            return;

        this.currentProfile = this.SettingsManager.GetProfileById(profile.Id);

        //
        // A thread which already exists has to carry the choice. Before the first message there is
        // none, and the choice then travels in the thread a new chat is started with.
        //
        if (this.ChatThread is not null)
        {
            this.ChatThread = this.ChatThread with
            {
                SelectedProfile = this.currentProfile.Id,
            };

            // The copy shares the draft, but storing the former thread would bring the former profile back:
            if (this.unstoredChat?.ChatId == this.ChatThread.ChatId)
                this.unstoredChat = this.ChatThread;

            this.draftAutosave?.Follow(this.ChatThread);
            await this.ChatThreadChanged.InvokeAsync(this.ChatThread);
        }
    }
    
    private async Task ChatTemplateWasChanged(ChatTemplate chatTemplate)
    {
        this.currentChatTemplate = this.SettingsManager.GetChatTemplateById(chatTemplate.Id);
        if(!string.IsNullOrWhiteSpace(this.currentChatTemplate.PredefinedUserPrompt))
            this.ComposerState.SetSystemInput(this.currentChatTemplate.PredefinedUserPrompt);

        // Apply template's file attachments (replaces existing):
        this.ComposerState.ReplaceFileAttachments(this.currentChatTemplate.FileAttachments);

        if (this.ChatThread is not null)
        {
            // Starting the new chat is what hands the selection of the new template to it:
            await this.StartNewChat(true);
            return;
        }

        //
        // Without a thread there is nothing to start anew, so the selection of the new template is
        // applied right here. It travels into the thread which the first message creates.
        //
        if (this.ApplyChatTemplateProfileAndProvider())
            await this.ProviderChanged.InvokeAsync(this.Provider);

        await this.ApplyChatTemplateToolSelectionAsync();
        this.ApplyAutomaticDataSourceOptions();
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

        this.Provider = this.SettingsManager.GetChatProviderForLoadedChat(this.Provider.Id);
        if (this.Provider != previousProvider)
            await this.ProviderChanged.InvokeAsync(this.Provider);

        if (this.ChatThread is null)
        {
            this.currentProfile = this.SettingsManager.GetPreselectedProfile(Tools.Components.CHAT);
            this.currentChatTemplate = this.SettingsManager.GetPreselectedChatTemplate(Tools.Components.CHAT);

            //
            // A chat which has not started yet keeps up with its chat template. The provider follows
            // only when the template now preselects another one, so that a provider the user picked
            // by hand survives a change of some unrelated setting:
            //
            if (previousChatTemplate.Id != this.currentChatTemplate.Id || previousChatTemplate.ProviderId != this.currentChatTemplate.ProviderId)
            {
                if (this.ApplyChatTemplateProfileAndProvider())
                    await this.ProviderChanged.InvokeAsync(this.Provider);
            }
            else if (this.SettingsManager.GetChatTemplateProfile(this.currentChatTemplate) is { } templateProfile)
                this.currentProfile = templateProfile;
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

        await this.ApplyUpdatedAutomaticDataSourceOptionsAfterConfigurationChange();
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

    /// <summary>
    /// Takes over what the user changed in the data source selection.
    /// </summary>
    /// <remarks>
    /// While an answer is being written, the selection is read-only and turns every change down
    /// itself, since the answer reads the very options it shows. Checked here once more, so that the
    /// chat never stores a change which reached it anyway.
    /// </remarks>
    private async Task DataSourceOptionsWereChanged(DataSourceOptions updatedOptions)
    {
        if (this.IsCurrentChatStreaming)
            return;

        await this.SetCurrentDataSourceOptions(updatedOptions);
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
        this.KeepComposerInDraft();
        this.hasUnsavedChanges = true;
    }

    private void ComposerAttachmentsChanged(HashSet<FileAttachment> attachments)
    {
        if (!ReferenceEquals(this.ComposerState.FileAttachments, attachments))
            this.ComposerState.ReplaceFileAttachments(attachments);

        this.ComposerState.MarkUserDraft();
        this.KeepComposerInDraft();
        this.hasUnsavedChanges = true;
    }

    /// <summary>
    /// Hands what the user changed in the composer to the draft of the open chat, and stores the chat
    /// once the user paused.
    /// </summary>
    /// <remarks>
    /// Only for changes of the user: a chat template fills the composer as well, but that is no message
    /// anybody started, so it never reaches the draft on its own. The draft follows the composer in
    /// every storage mode, so storing the chat by hand keeps the message, too. Storing it without being
    /// asked happens only while chats are stored automatically, though, because it stores every other
    /// change of the chat along with it.
    /// </remarks>
    private void KeepComposerInDraft()
    {
        if (this.ChatThread is null)
            return;

        this.MirrorComposerIntoDraft(this.ChatThread);
        if (this.SettingsManager.ConfigurationData.Workspace.StorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY)
            this.draftAutosave?.Schedule(this.ChatThread);
    }

    /// <summary>
    /// Puts the text and the attachments of the composer into the draft of the given chat.
    /// </summary>
    /// <param name="chat">The chat the message in the composer belongs to.</param>
    private void MirrorComposerIntoDraft(ChatThread chat)
    {
        chat.Draft.SetText(this.ComposerState.UserInput);
        chat.Draft.MirrorAttachments(this.ComposerState.FileAttachments);
    }

    /// <summary>
    /// Stores the chat being left with the message the user started in it, if that is still pending.
    /// </summary>
    private async Task FlushDraftAsync()
    {
        if (this.draftAutosave is not null)
            await this.draftAutosave.FlushAsync();
    }

    /// <summary>
    /// Stores a new chat which is being left, when the user started a message in it.
    /// </summary>
    /// <remarks>
    /// A new chat reaches the disk together with its first message. Whoever leaves it before would
    /// lose the message, so the chat is stored now, with the message as its draft. That happens only
    /// while chats are stored automatically, and only for a message of the user: the text of a chat
    /// template alone is nobody's message, and storing it would fill the list with chats nobody
    /// started.<br/><br/>
    ///
    /// A new chat comes in two kinds. Usually, it has no thread before its first message, and the
    /// message lives in the composer only. It becomes a disappearing chat of its own then. Starting a
    /// new chat in a workspace creates the thread right away instead, see unstoredChat. That one is
    /// stored with whatever its draft holds, and also while a transcript is still being made for it,
    /// since the transcript would otherwise arrive in a chat which is nowhere. Only when its workspace
    /// was deleted meanwhile, the chat goes along with it.<br/><br/>
    ///
    /// The composer is cleared before the chat is stored, since the message has moved into its chat.
    /// Leaving twice in a row, e.g., with two quick clicks, therefore stores it only once.
    /// </remarks>
    /// <param name="hadNoThread">Whether the chat being left had no thread, so its message is in the composer only.</param>
    private async Task PreserveNewChatDraftAsync(bool hadNoThread)
    {
        // Whatever happens below, the new chat is left now:
        var unstoredChatBeingLeft = this.unstoredChat;
        this.unstoredChat = null;

        if (this.SettingsManager.ConfigurationData.Workspace.StorageBehavior is not WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY)
            return;

        ChatThread chat;
        if (hadNoThread)
        {
            if (!this.ComposerState.HasVisibleUserDraft)
                return;

            this.RefreshCurrentProfileAndChatTemplate();
            chat = this.NewChatThread(this.ExtractThreadName(this.ComposerState.UserInput));
            this.MirrorComposerIntoDraft(chat);
            this.ComposerState.Clear();
        }
        else if (unstoredChatBeingLeft is not null && (!unstoredChatBeingLeft.Draft.IsEmpty || this.MediaTranscriptionService.IsBusy(MediaImportOwner.ForChat(unstoredChatBeingLeft.ChatId))))
        {
            // Deleting the workspace took the chat along. Storing it would bring the workspace back, as an unnamed one:
            if (!WorkspaceBehaviour.IsWorkspaceExisting(unstoredChatBeingLeft.WorkspaceId))
                return;

            chat = unstoredChatBeingLeft;
        }
        else
            return;

        try
        {
            await this.StoreAndListChatAsync(chat);
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, $"The new chat '{chat.ChatId}' could not be stored with the message the user started in it.");
        }
    }

    /// <summary>
    /// Stores the given chat, through the workspace component whenever there is one.
    /// </summary>
    /// <remarks>
    /// The workspace component updates its tree along with storing, so it lists a new chat right
    /// away, and a changed one with its new name and time.
    /// </remarks>
    /// <param name="chat">The chat to store.</param>
    private Task StoreAndListChatAsync(ChatThread chat) => this.Workspaces is not null ? this.Workspaces.StoreChatAsync(chat) : WorkspaceBehaviour.StoreChatAsync(chat);

    /// <summary>
    /// Stores a new chat before the user leaves the chat page, when the user started a message in it.
    /// </summary>
    /// <remarks>
    /// The page takes the composer along when it goes, and with it the message of a chat which has
    /// no thread yet. This is a handler of its own rather than a message of the bus: the navigation
    /// waits for its handlers, while the bus hands a message over without waiting, so the page could
    /// be gone before the chat is stored.<br/><br/>
    ///
    /// Only while chats are stored automatically. Otherwise, nothing is preserved anyway, and the
    /// navigation may still be canceled after the user was asked about unsaved changes, so the new
    /// chat has to stay as it is.
    /// </remarks>
    /// <param name="context">The navigation which is about to happen.</param>
    private async ValueTask OnLocationChangingAsync(LocationChangingContext context)
    {
        if (this.SettingsManager.ConfigurationData.Workspace.StorageBehavior is not WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY)
            return;

        // Opening the chat page again keeps the page, and the composer with it:
        if (string.Equals(this.NavigationManager.ToAbsoluteUri(context.TargetLocation).AbsolutePath, Routes.CHAT, StringComparison.OrdinalIgnoreCase))
            return;

        await this.PreserveNewChatDraftAsync(this.ChatThread is null);
    }

    /// <summary>Creates and stores a stable draft immediately after media import confirmation.</summary>
    private async Task<ChatThread?> EnsureMediaImportChatAsync(string firstMediaPath)
    {
        if (this.ChatThread is not null)
            return this.ChatThread;

        this.RefreshCurrentProfileAndChatTemplate();
        var promptName = this.ExtractThreadName(this.ComposerState.UserInput);
        var threadName = string.IsNullOrWhiteSpace(this.ComposerState.UserInput)
            ? string.Format(T("Transcription: {0}"), Path.GetFileName(firstMediaPath))
            : promptName;

        this.ChatThread = this.NewChatThread(threadName);

        // A message the user started belongs to the new chat, so it is stored with it:
        if (this.ComposerState.HasUserDraft)
            this.MirrorComposerIntoDraft(this.ChatThread);

        //
        // Like saving, this goes through the workspace component whenever there is one, so its tree
        // lists the new chat right away, together with the progress of the transcription, instead
        // of only after the first message was sent:
        //
        await this.StoreAndListChatAsync(this.ChatThread);
        this.MarkCurrentChatAsLoadedParameter();
        await this.ChatThreadChanged.InvokeAsync(this.ChatThread);
        await this.SyncForegroundChatAsync();
        return this.ChatThread;
    }
    
    private async Task SendMessage(bool reuseLastUserPrompt = false)
    {
        //
        // One answer at a time per chat. Checked before anything touches the chat or the composer:
        // the AI job service would turn a second answer down only after the chat got its message and
        // an empty answer, which then stays.
        //
        if (this.IsCurrentChatStreaming)
            return;

        if (this.MediaTranscriptionService.IsBusy(this.CurrentMediaImportOwner))
            return;

        await this.RefreshProviderSelectionFromConfigurationAsync();

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
            this.ChatThread = this.NewChatThread(this.ExtractThreadName(this.ComposerState.UserInput));
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
            this.ChatThread.Draft.Clear();

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
        {
            //
            // Regenerating asks again with the prompt that led to this answer. A thread which never
            // carried one -- a chat template whose example conversation holds AI blocks only -- has
            // nothing to reuse here. That is no reason to fail: the thread itself is what the model
            // is given, and everything downstream already reads a missing prompt as "no data source
            // lookup, just answer again".
            //
            lastUserPrompt = this.ChatThread.Blocks.LastOrDefault(x => x.Role is ChatRole.USER)?.Content;
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
        
        this.ChatThread?.Blocks.Add(new ContentBlock
        {
            Time = time,
            ContentType = ContentType.TEXT,
            Role = ChatRole.AI,
            Content = aiText,
        });
        
        //
        // Clear the input field, unless the answer is only generated again. The message the user
        // has started to write in the meantime is no part of that, so it stays where it is:
        //
        if (!reuseLastUserPrompt)
        {
            await this.inputField.FocusAsync();
            this.ComposerState.Clear();
            await this.inputField.BlurAsync();
        }

        // Enable the stream state for the chat component:
        this.hasUnsavedChanges = true;
        
        if (this.SettingsManager.ConfigurationData.Chat.ShowLatestMessageAfterLoading)
        {
            this.mustScrollToBottomAfterRender = true;
            this.scrollRenderCountdown = 2;
        }
        
        this.Logger.LogDebug($"Start processing user input using provider '{this.Provider.InstanceName}' with model '{this.Provider.Model}'.");
        this.StateHasChanged();
        this.ChatThread!.RuntimeComponent = Tools.Components.CHAT;
        this.ChatThread.RuntimeAssistantName = string.Empty;
        this.ChatThread.SelectedToolIds = [..this.selectedToolIds];
        this.ChatThread.RuntimeSelectedToolIds = this.ToolRegistry.FilterToolIdsForProvider(this.Provider, this.selectedToolIds, this.ChatThread.RequiredOutboundDataRestriction.Restriction);

        // From here on, the AI job stores the chat, in every storage mode:
        this.unstoredChat = null;
        await this.AIJobService.TryStartChatGenerationAsync(new ChatGenerationRequest
        {
            ChatThread = this.ChatThread,
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

    /// <summary>
    /// Takes over the tool selection of the chat that was just loaded or handed to this component.
    /// </summary>
    /// <remarks>
    /// A thread without a selection means the chat defaults: that is a chat saved before tools
    /// existed, as well as one a launcher opened without naming any. Both want what the settings
    /// preselect. Every path that puts a thread into this component has to come through here, or
    /// the footer would keep showing the tools of the chat before it.
    /// </remarks>
    private void ApplyToolSelectionOfLoadedChat() =>
        this.selectedToolIds = this.ChatThread?.SelectedToolIds is { } storedToolIds
            ? this.ToolRegistry.NormalizeSelection(storedToolIds)
            : this.ToolRegistry.GetDefaultToolIds(Tools.Components.CHAT);

    private void SelectedToolIdsChanged(HashSet<string> updatedToolIds)
    {
        this.selectedToolIds = this.ToolRegistry.NormalizeSelection(updatedToolIds);

        //
        // The thread keeps the selection so that reopening the chat tomorrow brings the same tools
        // back. What is stored is what the user chose, not what the current provider is allowed to
        // run: filtering here would quietly drop a tool for good the moment the user switches to a
        // provider with less confidence.
        //
        if (this.ChatThread is not null)
        {
            this.ChatThread.SelectedToolIds = [..this.selectedToolIds];
            this.hasUnsavedChanges = true;
        }
    }
    
    private async Task SaveThread()
    {
        if(this.ChatThread is null)
            return;
        
        if (!this.CanThreadBeSaved)
            return;

        await this.StoreAndListChatAsync(this.ChatThread);
        this.hasUnsavedChanges = false;

        // A new chat is on the disk now, like any other:
        this.unstoredChat = null;
    }
    
    /// <summary>
    /// Starts a new chat in the given workspace, on behalf of the workspace tree.
    /// </summary>
    /// <remarks>
    /// The chat starts just like one from the button for a new chat in the workspace: the user is
    /// asked about unsaved changes, and the chat gets its chat template with the data sources, tools,
    /// and example conversation the template asks for.
    /// </remarks>
    /// <param name="workspaceId">The ID of the workspace.</param>
    public Task StartNewChatInWorkspaceAsync(Guid workspaceId) => workspaceId == Guid.Empty
        ? Task.CompletedTask
        : this.StartNewChat(targetWorkspaceId: workspaceId);

    /// <summary>
    /// Starts a new chat in place of the open one.
    /// </summary>
    /// <remarks>
    /// Every way to start a new chat leads here: the buttons of the toolbar, a change of the chat
    /// template, and the workspace tree. So every new chat gets its chat template with the data
    /// sources, tools, and example conversation, its profile, and its provider in the same way, and
    /// the user is asked about unsaved changes in the same way.
    /// </remarks>
    /// <param name="useSameWorkspace">True starts the new chat with a thread right away, in the workspace of the open chat. False starts it outside any workspace, and its thread comes with the first message.</param>
    /// <param name="deletePreviousChat">Whether to delete the open chat first.</param>
    /// <param name="targetWorkspaceId">The workspace for the new chat, when the workspace tree asks for one. When given, the new chat gets a thread in that workspace, whatever useSameWorkspace says.</param>
    private async Task StartNewChat(bool useSameWorkspace = false, bool deletePreviousChat = false, Guid? targetWorkspaceId = null)
    {
        //
        // Where the new chat goes is decided right away, when it is asked for. Deleting the open chat
        // below takes it out of view, and with it the workspace this component remembers for the header:
        //
        var workspaceOfNewChat = targetWorkspaceId ?? this.currentWorkspaceId;

        //
        // Want the user to manage the chat storage manually? In that case, we have to ask the user
        // about possible data loss:
        //
        if (this.SettingsManager.ConfigurationData.Workspace.StorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_MANUALLY && this.hasUnsavedChanges && !this.IsCurrentChatStreaming)
        {
            var dialogParameters = new DialogParameters<ConfirmDialog>
            {
                { x => x.Message, T("Are you sure you want to start a new chat? All unsaved changes will be lost.") },
            };
        
            var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Start New Chat"), dialogParameters, DialogOptions.FULLSCREEN);
            var dialogResult = await dialogReference.Result;
            if (dialogResult is null || dialogResult.Canceled)
                return;
        }

        //
        // The chat being left keeps the message the user started in it. This comes before deleting
        // the chat, so that storing it can never bring it back afterward. A new chat which never
        // reached the disk is simply dropped when the user wants it deleted:
        //
        if (deletePreviousChat)
            this.unstoredChat = null;

        await this.FlushDraftAsync();
        await this.PreserveNewChatDraftAsync(this.ChatThread is null);

        //
        // Delete the previous chat when desired and necessary:
        //
        if (this.ChatThread is not null && deletePreviousChat)
        {
            //
            // A deleted chat cannot be restored, and the check above never covers this path: it
            // exists only while chats are stored automatically, while that check runs only while
            // they are stored manually. So we let the deletion itself ask, with the question the
            // chat list asks. When it reports the chat is still there, the user declined or the
            // chat is busy, and we stop before the reset below takes it out of view:
            //
            bool chatIsGone;
            if (this.Workspaces is null)
                chatIsGone = await WorkspaceBehaviour.DeleteChatAsync(this.DialogService, this.ChatThread.WorkspaceId, this.ChatThread.ChatId);
            else
            {
                var chatPath = this.ChatThread.WorkspaceId == Guid.Empty
                    ? Path.Join(SettingsManager.DataDirectory, "tempChats", this.ChatThread.ChatId.ToString())
                    : Path.Join(SettingsManager.DataDirectory, "workspaces", this.ChatThread.WorkspaceId.ToString(), this.ChatThread.ChatId.ToString());

                chatIsGone = await this.Workspaces.DeleteChatAsync(chatPath, unloadChat: true);
            }

            if (!chatIsGone)
                return;
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

        // What the chat template preselects comes last, so that it wins over the behavior above:
        if (this.ApplyChatTemplateProfileAndProvider())
            await this.ProviderChanged.InvokeAsync(this.Provider);

        //
        // Reset the chat thread or create a new one. A target workspace always means a new chat in
        // that workspace, no matter what useSameWorkspace says:
        //
        var startsOutsideWorkspaces = !useSameWorkspace && targetWorkspaceId is null;
        if (startsOutsideWorkspaces)
        {
            //
            // When the user wants to start a new chat outside the current workspace, we have to
            // reset the chat thread. The workspace id and the workspace name follow below:
            //
            this.ChatThread = null;
        }
        else
        {
            //
            // When the user wants to start a new chat in a workspace, the same one or the one the
            // workspace tree asked for, we have to reset the chat thread only. The new chat reaches
            // the disk only once there is something to keep. The header follows once the parent
            // hands the new thread back to us:
            //
            this.ChatThread = this.NewChatThread(string.Empty, workspaceOfNewChat);
            this.unstoredChat = this.ChatThread;
        }

        //
        // The parent hears about the new thread right away, before anything below waits. The thread
        // is a parameter of this component, and the parent hands over its own whenever it renders,
        // e.g., after it received the click on the workspace tree, or after it showed another
        // workspace name. Until the parent knows the new thread, such a render brings back the chat
        // being left, while the composer already holds what the chat template asks for. Marking the
        // thread as loaded first keeps the parameter update from taking it for another chat being
        // opened, which would clear the composer:
        //
        this.MarkCurrentChatAsLoadedParameter();
        await this.ChatThreadChanged.InvokeAsync(this.ChatThread);

        // Clearing the workspace name renders the parent as well, so it comes only now:
        if (startsOutsideWorkspaces)
            this.ClearWorkspaceHeaderState();

        this.ComposerState.ApplyTemplate(this.currentChatTemplate);

        // Now, the chat starts with what its template asks for, and with the chat defaults wherever
        // that template says nothing:
        await this.ApplyChatTemplateToolSelectionAsync();
        this.ApplyAutomaticDataSourceOptions();
        await this.SyncForegroundChatAsync();
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
        
            var confirmationDialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Unsaved Changes"), confirmationDialogParameters, DialogOptions.FULLSCREEN);
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
        
        //
        // Like saving and copying, the move goes through the workspace component whenever there is
        // one, so its tree shows the chat in the new place right away:
        //
        var previousWorkspaceId = this.ChatThread!.WorkspaceId;
        if (this.Workspaces is not null)
            await this.Workspaces.MoveChatAsync(this.ChatThread!, workspaceId);
        else
            await WorkspaceBehaviour.MoveChatAsync(this.ChatThread!, workspaceId);

        await this.FollowMovedChatAsync(previousWorkspaceId);
    }

    /// <summary>
    /// Follows the open chat into the workspace it was moved to.
    /// </summary>
    /// <remarks>
    /// A move is no other chat being opened, so the message being written stays in the composer,
    /// whether the user moved the chat from here or through the workspace tree. What has to change are
    /// the paths of its attachments: the move took the chat directory along, and with it the
    /// transcripts of audio and video files. Left with their old paths, the composer would send
    /// references to files which are gone.
    /// </remarks>
    /// <param name="previousWorkspaceId">The workspace the chat came from, or Guid.Empty for a temporary chat.</param>
    private async Task FollowMovedChatAsync(Guid previousWorkspaceId)
    {
        var chatId = this.ChatThread!.ChatId;
        var workspaceId = this.ChatThread.WorkspaceId;
        var relocatedAttachments = this.ComposerState.FileAttachments
            .Select(attachment => WorkspaceBehaviour.RelocateAttachmentAfterMove(attachment, chatId, previousWorkspaceId, workspaceId))
            .ToList();

        this.ComposerState.FileAttachments.Clear();
        foreach (var attachment in relocatedAttachments)
            this.ComposerState.FileAttachments.Add(attachment);

        this.MarkCurrentChatAsLoadedParameter();
        await this.SyncWorkspaceHeaderWithChatThreadAsync();
    }

    /// <summary>
    /// Copies the open chat and continues in the copy.
    /// </summary>
    /// <remarks>
    /// Copied is the chat as it stands on the screen, not the state which was stored last. That is
    /// why nothing is asked about unsaved changes here, unlike everywhere else a chat is left
    /// behind: none of them are lost, they move into the copy, while the original keeps what was
    /// stored.<br/><br/>
    ///
    /// The same holds for the message being written. The copy is marked as loaded right away,
    /// because the parameter update which follows would otherwise take it for another chat being
    /// opened and clear the composer. Only the transcripts attached to that message are exchanged:
    /// those of the original live in its directory, and the copy got copies of its own.
    /// </remarks>
    private async Task CopyCurrentChat()
    {
        if (this.ChatThread is null || !this.CanThreadBeCopied)
            return;

        var sourceChat = this.ChatThread;
        var copy = this.Workspaces is null
            ? await WorkspaceBehaviour.CopyChatAsync(this.DialogService, sourceChat)
            : await this.Workspaces.CopyChatAsync(sourceChat);

        if (copy is null)
            return;

        var transcriptsOfTheOriginal = sourceChat.Draft.MediaTranscripts.Select(transcript => transcript.FilePath).ToHashSet(StringComparer.Ordinal);
        this.ComposerState.FileAttachments.RemoveWhere(attachment => transcriptsOfTheOriginal.Contains(attachment.FilePath));
        foreach (var transcript in copy.Draft.MediaTranscripts)
            this.ComposerState.FileAttachments.Add(transcript);

        this.ChatThread = copy;
        this.hasUnsavedChanges = false;

        // The copy carries the message along, so a new chat which never reached the disk stays that way:
        this.unstoredChat = null;

        await this.SyncForegroundChatAsync();
        this.MarkCurrentChatAsLoadedParameter();
        await this.SyncWorkspaceHeaderWithChatThreadAsync();
        await this.ChatThreadChanged.InvokeAsync(this.ChatThread);
    }

    private async Task LoadedChatChanged(bool notifyParent = true)
    {
        this.hasUnsavedChanges = false;
        this.ComposerState.Clear();

        if (this.ChatThread is not null)
        {
            this.ChatThread = this.AIJobService.TryGetLiveChatThread(this.ChatThread.ChatId) ?? this.ChatThread;

            // The message the user had started in this chat comes back:
            this.ComposerState.RestoreFromDraft(this.ChatThread.Draft);
            this.loadedParameterChatId = this.ChatThread.ChatId;
            this.loadedParameterWorkspaceId = this.ChatThread.WorkspaceId;
            if (notifyParent)
                await this.ChatThreadChanged.InvokeAsync(this.ChatThread);

            await this.SyncWorkspaceHeaderWithChatThreadAsync();
            await this.SyncForegroundChatAsync();
            this.dataSourceSelectionComponent?.ChangeOptionWithoutSaving(this.ChatThread.DataSourceOptions, this.ChatThread.AISelectedDataSources);
            this.ApplyToolSelectionOfLoadedChat();
        }
        else
        {
            this.loadedParameterChatId = Guid.Empty;
            this.loadedParameterWorkspaceId = Guid.Empty;
            this.ClearWorkspaceHeaderState();
            await this.SyncForegroundChatAsync();
            this.ApplyAutomaticDataSourceOptions();
        }

        await this.SelectProviderWhenLoadingChat();
        if (this.SettingsManager.ConfigurationData.Chat.ShowLatestMessageAfterLoading)
        {
            this.mustScrollToBottomAfterRender = true;
            this.scrollRenderCountdown = 2;
        }
        
        this.StateHasChanged();
    }

    private async Task RefreshProviderSelectionFromConfigurationAsync()
    {
        var updatedProvider = this.SettingsManager.GetPreselectedProvider(Tools.Components.CHAT, this.Provider.Id);
        var providerChanged = updatedProvider != this.Provider;
        if (providerChanged)
            this.Provider = updatedProvider;

        if (!providerChanged)
            return;

        await this.ProviderChanged.InvokeAsync(this.Provider);
    }
    
    private async Task ResetState()
    {
        await this.FlushDraftAsync();
        await this.PreserveNewChatDraftAsync(this.ChatThread is null);
        this.hasUnsavedChanges = false;
        this.ComposerState.Clear();
        this.ClearWorkspaceHeaderState();
        
        this.ChatThread = null;
        this.MarkCurrentChatAsLoadedParameter();
        await this.SyncForegroundChatAsync();
        this.ApplyAutomaticDataSourceOptions();
        await this.ChatThreadChanged.InvokeAsync(this.ChatThread);
    }

    private async Task SelectProviderWhenLoadingChat()
    {
        var chatProvider = this.ChatThread?.SelectedProvider;
        var chatProfile = this.ChatThread?.SelectedProfile;
        var chatChatTemplate = this.ChatThread?.SelectedChatTemplate;

        this.Provider = this.SettingsManager.GetChatProviderForLoadedChat(chatProvider);

        // A chat without a thread has not started yet, so it starts with what its chat template preselects:
        if (this.ChatThread is null)
            this.ApplyChatTemplateProfileAndProvider();

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
        if (!this.blockActions.TryRemove(this.ChatThread, block))
            return;

        this.hasUnsavedChanges = true;
        await this.SaveThread();
        this.StateHasChanged();
    }

    private async Task RegenerateBlock(IContent aiBlock)
    {
        if(!this.ChatThread.IsLLMProviderAllowed(this.Provider))
            return;

        if (!this.blockActions.TryRemoveForRegenerate(this.ChatThread, aiBlock))
            return;

        this.hasUnsavedChanges = true;
        this.StateHasChanged();
        
        await this.SendMessage(reuseLastUserPrompt: true);
    }

    private async Task RollbackBlock(IContent aiBlock)
    {
        // Which parts of the thread a rollback resets and which it keeps is documented at RollBackTo:
        if (!this.blockActions.TryRollBack(this.ChatThread, aiBlock))
            return;

        // The rollback reset the AI-selected data sources, which the data source selection still shows:
        this.dataSourceSelectionComponent?.ChangeOptionWithoutSaving(this.ChatThread.DataSourceOptions, this.ChatThread.AISelectedDataSources);
        this.hasUnsavedChanges = true;
        await this.SaveThread();
        this.tokenTracker?.Nudge();
        this.StateHasChanged();
        await this.inputField.FocusAsync();
    }
    
    private Task EditLastUserBlock(IContent block)
    {
        if (!this.blockActions.TryEditLastUserBlock(this.ChatThread, block))
            return Task.CompletedTask;

        this.RestoreComposerForEditing();
        this.hasUnsavedChanges = true;
        this.StateHasChanged();
        return Task.CompletedTask;
    }

    private Task EditLastBlock(IContent block)
    {
        if (!this.blockActions.TryEditLastBlock(this.ChatThread, block))
            return Task.CompletedTask;

        this.RestoreComposerForEditing();
        this.hasUnsavedChanges = true;
        this.StateHasChanged();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Puts the message being edited into the composer, after the chat made it its draft again.
    /// </summary>
    /// <remarks>
    /// Taken from the draft rather than from the message, so the composer also shows the transcripts
    /// the draft held before. The chat is then stored like after any other change of the draft: who
    /// edits a message and goes elsewhere finds it in the composer when coming back.
    /// </remarks>
    private void RestoreComposerForEditing()
    {
        this.ComposerState.RestoreFromDraft(this.ChatThread!.Draft);
        this.KeepComposerInDraft();
    }
    
    /// <summary>
    /// Works out what the next request would take out of the model's context window.
    /// </summary>
    /// <remarks>
    /// The whole conversation, not only what is being typed. A number counting the draft alone
    /// answers a question nobody asks: what decides whether the next message fits is everything
    /// which travels with it, and in a chat of any age the draft is the smallest part of that.
    ///
    /// This used to run only for providers with a tokenizer of their own, which is almost nobody,
    /// so almost nobody ever saw a number. The runtime falls back to the tokenizer shipped with AI
    /// Studio when a provider names none, so the count is available everywhere -- it is then an
    /// estimate, and it says so.
    ///
    /// Read the text from the bound property rather than from the input field: the field is a
    /// component reference, which is only set once the component has rendered.
    ///
    /// Called by the tracker, never directly. Whoever thinks something changed nudges it instead,
    /// and it decides when the work is worth doing.
    /// </remarks>
    /// <param name="token">Ends the count when the component goes away.</param>
    private async Task RecountTokensAsync(CancellationToken token)
    {
        var provider = AIStudio.Settings.Provider.NONE;
        var parts = ConversationParts.NOTHING;
        var reported = ReportedHistory.UNKNOWN;

        //
        // Semantic Search offers itself rather than being selected, and the registry answers
        // whether it can be offered asynchronously. So this is asked before collecting, which only
        // reads the provider and the choice of the chat, nothing a background job appends to:
        //
        var offersSemanticSearch = await this.OffersSemanticSearchAsync();

        //
        // Collected on the render thread, counted off it. Counting may take an IPC call per text,
        // and while it runs, the background job which writes the answer appends to the very list
        // which is walked here.
        //
        await this.InvokeAsync(() =>
        {
            //
            // Before the first message there is no thread yet, so what is measured is the one a new
            // chat would start with. A preselected profile or a chat template is already part of
            // that, and it may even bring an example conversation along -- reporting nothing for all
            // of it would tell a person their window is empty while their first message is not.
            //
            var thread = this.ChatThread ?? this.NewChatThread(string.Empty);
            var toolDefinitions = this.GetRunnableToolDefinitions(offersSemanticSearch);
            provider = this.Provider;
            parts = ConversationParts.Of(thread, this.BuildSystemPromptFor(thread, toolDefinitions), this.UserInput, this.ComposerState.FileAttachments, provider.SupportsImageInput(), toolDefinitions);
            reported = thread.ReportedHistoryFor(provider.Model);
        });

        var counted = await this.ConversationTokenCounter.CountAsync(provider, parts, reported, token);
        if (token.IsCancellationRequested)
            return;

        await this.InvokeAsync(() =>
        {
            if (counted == this.conversationTokens)
                return;

            this.conversationTokens = counted;
            this.StateHasChanged();
        });
    }

    /// <summary>
    /// Works out the system prompt a thread would send.
    /// </summary>
    /// <remarks>
    /// Not the prompt a person typed: a chat template may replace it, the retrieved data of a data
    /// source is appended to it, the selected profile adds a paragraph, and the policy of the
    /// selected tools adds another. Switching a profile while writing therefore moves the number,
    /// which is the whole reason this is asked rather than read off the thread.
    /// </remarks>
    /// <param name="thread">The thread to build the prompt for.</param>
    /// <param name="toolDefinitions">The tools whose policy the prompt states.</param>
    /// <returns>The system prompt as it would be sent.</returns>
    private string BuildSystemPromptFor(ChatThread thread, IReadOnlyList<ToolDefinition> toolDefinitions) => thread.BuildSystemPrompt(this.SettingsManager, toolDefinitions).Text;

    /// <summary>
    /// The tools the next request would offer the model.
    /// </summary>
    /// <remarks>
    /// Filtered for the provider and the chat the same way they are before sending, so that a tool
    /// the provider is not trusted enough to receive does not count, and neither does one which the
    /// internal content of the chat keeps back. A new chat holds none, so nothing is kept back.
    ///
    /// Asked for once and used twice: their policy goes into the system prompt, and their schemas
    /// travel next to it in the request body. Both cost tokens, and both change the moment somebody
    /// switches a tool on.
    ///
    /// Semantic Search is no selected tool, so it comes on top when it is offered. It counts with
    /// its static definition: the one a request offers lists the data sources as well, which only
    /// the request asks for.
    ///
    /// Read Web Page, Search Confluence, and Search Mails likewise count with their registered
    /// instructions, which assume the default settings and a chat which holds no internal content,
    /// and warn that internal content ends web search. A request words them after the actual settings, the chat,
    /// and the tools it offers, so the count comes out a few tokens off. One case it gets wrong as a
    /// whole: a chat restricted to the configured services only, with neither a wiki nor allowed
    /// private hosts configured, leaves Read Web Page nothing to offer, and only the request finds
    /// that out, see ReadWebPageTool.ResolveFunctionAsync.
    /// </remarks>
    /// <param name="offersSemanticSearch">Whether the next request offers Semantic Search, see OffersSemanticSearchAsync.</param>
    /// <returns>The definitions of the selected tools, and of Semantic Search when it is offered.</returns>
    private IReadOnlyList<ToolDefinition> GetRunnableToolDefinitions(bool offersSemanticSearch)
    {
        var outboundDataRestriction = (this.ChatThread?.RequiredOutboundDataRestriction ?? OutboundDataRequirement.NONE).Restriction;
        var definitions = this.ToolRegistry.FilterToolIdsForProvider(this.Provider, this.selectedToolIds, outboundDataRestriction)
            .Select(this.ToolRegistry.GetDefinition)
            .Where(definition => definition is not null)
            .Select(definition => definition!)
            .ToList();

        if (offersSemanticSearch && this.ToolRegistry.GetDefinition(ToolSelectionRules.SEMANTIC_SEARCH_TOOL_ID) is { } semanticSearch)
            definitions.Add(semanticSearch);

        return definitions;
    }

    /// <summary>
    /// Whether the next request offers the model Semantic Search, as far as this can be told without asking the data sources.
    /// </summary>
    /// <remarks>
    /// An estimate on purpose. Whether a data source can be searched right now would mean asking
    /// every ERI server with every count, and the count runs all the time. Once the first answer is
    /// there, the number the provider reported takes over anyway, see ChatThread.ReportedHistoryFor.
    /// </remarks>
    /// <returns>True when the next request offers Semantic Search, as far as can be told.</returns>
    private async Task<bool> OffersSemanticSearchAsync()
    {
        var options = this.GetCurrentDataSourceOptions();
        if (!PreviewFeatures.PRE_RAG_2024.IsEnabled(this.SettingsManager) || !options.IsEnabled())
            return false;

        var retrievalMode = await this.ToolRegistry.GetEffectiveRetrievalModeAsync(options, this.Provider, Tools.Components.CHAT);
        return retrievalMode.Mode is DataSourceRetrievalMode.SEMANTIC_SEARCH;
    }

    /// <summary>
    /// The thread a new chat starts with, as the selections made so far decide it.
    /// </summary>
    /// <remarks>
    /// In one place because three code paths used to write it out, and because the token count has
    /// to measure the same thing they build. A count against a thread assembled differently from
    /// the one which is then sent would be wrong in exactly the moment a person looks at it: before
    /// they send their first message.
    ///
    /// The data source options are those chosen before the chat had a thread. Starting a new chat in
    /// a workspace replaces them right afterward with those its chat template asks for.
    /// </remarks>
    /// <param name="name">The name of the thread.</param>
    /// <param name="workspaceId">The workspace of the thread. Without one, the thread goes to the workspace of the open chat.</param>
    /// <returns>The new thread.</returns>
    private ChatThread NewChatThread(string name, Guid? workspaceId = null) => new()
    {
        DataSourceOptions = this.earlyDataSourceOptions,
        IncludeDateTime = true,
        SelectedProvider = this.Provider.Id,
        SelectedProfile = this.currentProfile.Id,
        SelectedChatTemplate = this.currentChatTemplate.Id,
        SelectedToolIds = [..this.selectedToolIds],
        SystemPrompt = SystemPrompts.DEFAULT,
        WorkspaceId = workspaceId ?? this.currentWorkspaceId,
        ChatId = Guid.NewGuid(),
        Name = name,
        Blocks = this.currentChatTemplate == ChatTemplate.NO_CHAT_TEMPLATE ? [] : this.currentChatTemplate.ExampleConversation.Select(block => block.DeepClone()).ToList(),
    };
    
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
                await this.RefreshCulture();
                await this.RefreshChatSelectionsAfterConfigurationChange();
                this.tokenTracker?.Nudge();
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

    protected override Task<MessageBusAnswer<TResult>> ProcessIncomingMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data) where TPayload : default
    {
        switch (triggeredEvent)
        {
            case Event.HAS_CHAT_UNSAVED_CHANGES:
                if(this.SettingsManager.ConfigurationData.Workspace.StorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY)
                    return Task.FromResult(new MessageBusAnswer<TResult>(true, (TResult) (object) false));

                if (this.IsCurrentChatStreaming)
                    return Task.FromResult(new MessageBusAnswer<TResult>(true, (TResult) (object) false));

                //
                // A message the user started counts as unsaved on its own only while there is no chat
                // to keep it. Otherwise, it is part of the chat: changing it counts as a change of the
                // chat, and a message which came back from the disk with the chat is saved already.
                //
                return Task.FromResult(new MessageBusAnswer<TResult>(true, (TResult)(object)(this.hasUnsavedChanges || (this.ChatThread is null && this.ComposerState.HasVisibleUserDraft))));
        }

        return Task.FromResult(MessageBusAnswer<TResult>.NONE);
    }

    #endregion
    
    #region Overrides of MSGComponentBase

    protected override async ValueTask DisposeResourcesAsync()
    {
        this.MediaTranscriptionService.StateChanged -= this.OnMediaImportStateChanged;
        this.locationChangingRegistration?.Dispose();

        if (this.tokenTracker is not null)
            await this.tokenTracker.DisposeAsync();

        // Stores what is still pending, before the chat is left:
        if (this.draftAutosave is not null)
            await this.draftAutosave.DisposeAsync();

        //
        // So does a new chat started in a workspace, since the next component of the page, e.g.,
        // after the workspace sidebar was shown or hidden, cannot tell it from a deleted one. A chat
        // without a thread stays in the composer, though: the page keeps that, and its next
        // component goes on with it. Leaving the page stores it, see OnLocationChangingAsync.
        //
        await this.PreserveNewChatDraftAsync(hadNoThread: false);

        if(this.SettingsManager.ConfigurationData.Workspace.StorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY)
        {
            await this.SaveThread();
            this.hasUnsavedChanges = false;
        }

        await this.AIJobService.SetForegroundAsync(AIJobKind.CHAT_GENERATION, this.foregroundChatId, false);
    }

    #endregion
}
