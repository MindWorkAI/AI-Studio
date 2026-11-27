using AIStudio.Chat;
using AIStudio.Chat.Compression;
using AIStudio.Chat.Embedded;
using AIStudio.Dialogs;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.AssistantLaunch;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

/// <summary>
/// A chat which an assistant shows on its own page, such as one of the two chats of a task of the Model Comparison
/// assistant.
/// </summary>
/// <remarks>
/// The chat itself lives in EmbeddedChatService, so an answer goes on while the user looks elsewhere; this component
/// shows it and renders whenever the service says it changed. What a request carries beside the conversation, such as
/// the system prompt and the tools, the assistant decides through PrepareAsync. The count of tokens leaves out the
/// context window of the model by default: it would tell models apart which the user compares without knowing them.
/// An assistant whose requests carry long instructions shows it, see ShowsContextWindow. Either way, the chat sends no
/// message which no longer fits.
/// </remarks>
public partial class EmbeddedChat : MSGComponentBase
{
    /// <summary>
    /// Gets or sets where the chat lives.
    /// </summary>
    [Parameter, EditorRequired]
    public IEmbeddedChatStore Store { get; set; } = null!;

    /// <summary>
    /// Gets or sets the assistant which shows the chat.
    /// </summary>
    [Parameter, EditorRequired]
    public Tools.Components Component { get; set; }

    /// <summary>
    /// Gets or sets the ID of the chat.
    /// </summary>
    [Parameter, EditorRequired]
    public Guid ChatId { get; set; }

    /// <summary>
    /// Gets or sets how a chat which has no stored thread yet starts.
    /// </summary>
    [Parameter, EditorRequired]
    public Func<ChatThread> CreateThread { get; set; } = () => new();

    /// <summary>
    /// Gets or sets the provider which answers.
    /// </summary>
    [Parameter]
    public AIStudio.Settings.Provider Provider { get; set; } = AIStudio.Settings.Provider.NONE;

    /// <summary>
    /// Gets or sets how a request is readied before the message goes in, see EmbeddedChatRequest.PrepareAsync.
    /// </summary>
    [Parameter]
    public Func<ChatThread, Task<EmbeddedChatPreparation>> PrepareAsync { get; set; } = _ => Task.FromResult(EmbeddedChatPreparation.READY);

    /// <summary>
    /// Gets or sets whether messages to the user leave out which provider and which model answer.
    /// </summary>
    [Parameter]
    public bool HidesModelIdentity { get; set; }

    /// <summary>
    /// Gets or sets whether the chat only shows its messages, without a field to write another one.
    /// </summary>
    [Parameter]
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// Gets or sets whether the user may start the conversation over, which deletes it. A chat which this version cannot
    /// open then offers that as well.
    /// </summary>
    [Parameter]
    public bool AllowsReset { get; set; }

    /// <summary>
    /// Gets or sets the question before the conversation starts over, e.g., which names what stays as it is; a general
    /// question when left out.
    /// </summary>
    [Parameter]
    public string? ResetQuestion { get; set; }

    /// <summary>
    /// Gets or sets whether the count of tokens shows how much of the context window of the model the conversation
    /// fills: it warns when most of it is used, and before sending a message which nearly fills the window, the chat
    /// asks what to do. Leave it out where the user must not tell the models apart.
    /// </summary>
    /// <remarks>
    /// Either way, the chat sends no message after which the next answer would not fit anymore, see
    /// EmbeddedChatTokenBudget.NoticeBeforeSending. Where it hides the window, it only says that the conversation is
    /// full, see FullContextHint.
    /// </remarks>
    [Parameter]
    public bool ShowsContextWindow { get; set; }

    /// <summary>
    /// Gets or sets whether the chat offers to compress its conversation once it nearly fills the context window, in
    /// the notice before sending; only together with ShowsContextWindow, since the notice names the window. The summary
    /// then takes the place of the earlier messages, since the chat has no workspace to keep them in, see
    /// ChatCompressionTarget.SAME_CHAT.
    /// </summary>
    /// <remarks>
    /// No button offers it otherwise: the earlier messages are gone afterward, so the chat offers it only when the
    /// conversation would not go on without it.
    /// </remarks>
    [Parameter]
    public bool AllowsCompression { get; set; }

    /// <summary>
    /// Gets or sets what the chat says once its conversation fills the context window of the model, in place of its own
    /// advice, e.g., where the assistant offers another way on; null for the advice of the chat.
    /// </summary>
    [Parameter]
    public string? FullContextHint { get; set; }

    /// <summary>
    /// Gets or sets how the count of tokens gets the system prompt of the next request, for an assistant which puts it
    /// together anew for every request in PrepareAsync; null as its result means it cannot be known now, and the count
    /// stays unknown. Without it, the count takes the system prompt the chat stores, which is the one of its last
    /// request.
    /// </summary>
    /// <remarks>
    /// It must not fail: whatever goes wrong, sending reports anyway.
    /// </remarks>
    [Parameter]
    public Func<Task<string?>>? SystemPromptForCountAsync { get; set; }

    /// <summary>
    /// Gets or sets a number which changes whenever what the next request carries beside the conversation changed,
    /// e.g., material of the assistant in its system prompt; the chat counts its tokens again then.
    /// </summary>
    [Parameter]
    public int ContextVersion { get; set; }

    /// <summary>
    /// Gets or sets whether the user may attach files to a message.
    /// </summary>
    [Parameter]
    public bool AllowsAttachments { get; set; } = true;

    /// <summary>
    /// Gets or sets which files the user may attach, or null for every file a chat takes.
    /// </summary>
    [Parameter]
    public FileTypeFilter[]? AllowedFileTypes { get; set; }

    /// <summary>
    /// Gets or sets a message which the chat sends by itself when it shows a conversation without any message of the
    /// user, e.g., the first message of another chat on the same task. Sent once per chat, when the chat loads or, when
    /// the message arrives later, as soon as it does; when it cannot be sent, it waits in the field, so that the user
    /// can send it. See EmbeddedChatOpening.SendsNow.
    /// </summary>
    /// <remarks>
    /// The chat stores the message as it is, so give it a copy of its own, see ContentText.DeepClone.
    /// </remarks>
    [Parameter]
    public ContentText? OpeningMessage { get; set; }

    /// <summary>
    /// Gets or sets a request which lets the model open the conversation, e.g., by asking for what it needs. While the
    /// chat holds no message the user sees, it offers a button which sends the request; the user does not see the
    /// request, and may write first instead. Leave OpeningMessage out with it.
    /// </summary>
    [Parameter]
    public string? StartRequest { get; set; }

    /// <summary>
    /// Gets or sets a heading above the chat, or nothing where the place of the chat names it already.
    /// </summary>
    [Parameter]
    public string? Heading { get; set; }

    /// <summary>
    /// Gets or sets why the provider cannot answer, or null when it can.
    /// </summary>
    [Parameter]
    public string? ProviderIssue { get; set; }

    /// <summary>
    /// Gets or sets what the chat says before its conversation begins, or nothing.
    /// </summary>
    [Parameter]
    public string? Introduction { get; set; }

    /// <summary>
    /// Gets or sets the name a message gets when the user exports it.
    /// </summary>
    [Parameter]
    public string ExportFileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a message the user sends with one click once the model has answered, such as asking for the
    /// evaluation at the end of an exercise; null for none.
    /// </summary>
    [Parameter]
    public EmbeddedChatQuickMessage? QuickMessage { get; set; }

    [Inject]
    private EmbeddedChatService ChatService { get; init; } = null!;

    [Inject]
    private ConversationTokenCounter TokenCounter { get; init; } = null!;

    [Inject]
    private AssistantLaunchService AssistantLaunchService { get; init; } = null!;

    [Inject]
    private ChatCompressionService CompressionService { get; init; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private ILogger<EmbeddedChat> Logger { get; init; } = null!;

    /// <summary>
    /// Ends a count when the component goes away.
    /// </summary>
    private readonly CancellationTokenSource countCancellation = new();

    /// <summary>
    /// The token of countCancellation, taken while the source still lives.
    /// </summary>
    /// <remarks>
    /// A count goes on after an await, when the component may have gone away meanwhile, e.g., because the user moved on
    /// to another step of an assistant. The source is disposed by then and refuses to hand out its token; the token
    /// taken before still tells that the count was cancelled.
    /// </remarks>
    private CancellationToken countToken;

    /// <summary>
    /// What the next request carries, as far as it was counted.
    /// </summary>
    private ConversationTokens tokens = ConversationTokens.UNAVAILABLE;

    /// <summary>
    /// The provider the tokens were counted for, to count again when the parent chooses another one.
    /// </summary>
    private string countedProviderId = string.Empty;

    /// <summary>
    /// The version of what a request carries beside the conversation which the tokens were counted for, see
    /// ContextVersion.
    /// </summary>
    private int countedContextVersion;

    /// <summary>
    /// The chat shown, or null while it loads.
    /// </summary>
    private EmbeddedConversation? conversation;

    /// <summary>
    /// The ID of the chat shown, to notice when the parent shows another one.
    /// </summary>
    private Guid shownChatId;

    /// <summary>
    /// The ID of the chat which sent its opening message already, so that it sends the message only once.
    /// </summary>
    private Guid openedChatId;

    /// <summary>
    /// The message the user writes.
    /// </summary>
    private string userInput = string.Empty;

    /// <summary>
    /// The files attached to the message the user writes.
    /// </summary>
    private HashSet<FileAttachment> attachments = [];

    /// <summary>
    /// The attributes of the message field, such as the spell checking the user chose.
    /// </summary>
    private readonly Dictionary<string, object?> inputAttributes = new();

    /// <summary>
    /// Whether the summary of the conversation is taking the place of its earlier messages right now, see CompressAsync.
    /// </summary>
    /// <remarks>
    /// Nothing may be sent meanwhile: the message would go into the conversation which is being replaced.
    /// </remarks>
    private bool isCompressing;

    private bool IsRunning => this.conversation?.IsRunning ?? false;

    /// <summary>
    /// How much room the conversation leaves in the context window, as far as the chat shows it, see
    /// EmbeddedChatTokenBudget.Shown.
    /// </summary>
    private EmbeddedChatTokenBudgetState Budget => EmbeddedChatTokenBudget.Shown(this.tokens, this.ShowsContextWindow, ChatCompressionOptions.Of(this.SettingsManager.ConfigurationData.ChatCompression).ShareToAsk);

    /// <summary>
    /// Whether the chat offers to compress the conversation, see AllowsCompression.
    /// </summary>
    private bool OffersCompression => this.AllowsCompression && this.ShowsContextWindow;

    /// <summary>
    /// Whether the user may send a message. Where the chat shows the context window, a full one does not stop it:
    /// sending asks what to do then, see ConfirmContextWindowAsync. Where it hides the window, nothing could be asked,
    /// so a full conversation takes no more messages.
    /// </summary>
    private bool CanSend => this.conversation is { IsAvailable: true, IsRunning: false }
                            && !this.isCompressing
                            && this.ProviderIssue is null
                            && (this.ShowsContextWindow || this.Budget is not EmbeddedChatTokenBudgetState.FULL);

    private bool CanSendMessage => this.CanSend && !string.IsNullOrWhiteSpace(this.userInput);

    private bool CanReset => this.AllowsReset && !this.IsReadOnly;

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.countToken = this.countCancellation.Token;
        this.ChatService.ConversationChanged += this.OnConversationChangedAsync;
        this.SettingsManager.InjectSpellchecking(this.inputAttributes);
        await base.OnInitializedAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        if (this.shownChatId != this.ChatId)
        {
            this.shownChatId = this.ChatId;
            this.conversation = null;
            this.userInput = string.Empty;
            this.attachments = [];
            this.conversation = await this.ChatService.GetAsync(this.Store, this.Component, this.ChatId, this.CreateThread);
            await this.RecountAsync();
        }
        else if (this.countedProviderId != this.Provider.Id || this.countedContextVersion != this.ContextVersion)
            await this.RecountAsync();

        // Not awaited: the answer arrives through ConversationChanged, and the parameters of the next render must not wait for it.
        if (EmbeddedChatOpening.SendsNow(this.OpeningMessage, this.conversation, this.openedChatId == this.ChatId))
        {
            this.openedChatId = this.ChatId;
            _ = this.SendOpeningMessageAsync(this.OpeningMessage!);
        }
    }

    protected override void DisposeResources()
    {
        this.ChatService.ConversationChanged -= this.OnConversationChangedAsync;
        this.countCancellation.Cancel();
        this.countCancellation.Dispose();
        base.DisposeResources();
    }

    #endregion

    /// <summary>
    /// Renders the chat again when the service changed it, and counts again once an answer is complete.
    /// </summary>
    /// <param name="chatId">The ID of the chat which changed.</param>
    private Task OnConversationChangedAsync(Guid chatId)
    {
        if (chatId != this.ChatId)
            return Task.CompletedTask;

        return this.InvokeAsync(async () =>
        {
            if (!this.IsRunning)
                await this.RecountAsync();

            this.StateHasChanged();
        });
    }

    /// <summary>
    /// Counts what the conversation carries so far, while no answer runs.
    /// </summary>
    /// <remarks>
    /// The system prompt counts as SystemPromptForCountAsync gives it, otherwise as the chat stores it: what
    /// PrepareAsync adds for a request is not known before then. The count is therefore an estimate, and the message
    /// being written is left out.
    /// </remarks>
    private async Task RecountAsync()
    {
        this.countedProviderId = this.Provider.Id;
        this.countedContextVersion = this.ContextVersion;
        if (this.IsReadOnly || this.conversation is not { Thread: { } thread, IsRunning: false } || this.ProviderIssue is not null || this.Provider.UsedLLMProvider is LLMProviders.NONE)
        {
            this.tokens = ConversationTokens.UNAVAILABLE;
            return;
        }

        try
        {
            var systemPrompt = this.SystemPromptForCountAsync is { } systemPromptForCountAsync ? await systemPromptForCountAsync() : thread.SystemPrompt;
            if (systemPrompt is null)
            {
                this.tokens = ConversationTokens.UNAVAILABLE;
                return;
            }

            var parts = ConversationParts.Of(thread, systemPrompt, string.Empty, null, false, null);
            this.tokens = await this.TokenCounter.CountAsync(this.Provider, parts, thread.ReportedHistoryFor(this.Provider.Model), this.countToken);
        }
        catch (OperationCanceledException)
        {
            // The component went away.
        }
    }

    /// <summary>
    /// Gets about how many tokens the conversation carries, with how full the context of the model is when the chat shows
    /// it, or null when nothing could be counted.
    /// </summary>
    private string? TokenText
    {
        get
        {
            if (!this.tokens.IsKnown)
                return null;

            var culture = Tools.PluginSystem.I18N.I.Culture;
            var counted = TokenAmount.Format(this.tokens.Tokens, culture);
            if (!this.ShowsContextWindow)
            {
                var approximately = string.Format(T("approx. {0} tokens"), counted);
                return this.Budget is EmbeddedChatTokenBudgetState.FULL ? $"{approximately}. {this.FullContextText}" : approximately;
            }

            if (!this.tokens.Window.IsKnown)
                return string.Format(T("approx. {0} tokens; the context window of this model is unknown"), counted);

            var window = TokenAmount.Format(this.tokens.Window.DefaultTokens, culture);
            var budget = string.Format(this.tokens.IsEstimate && !this.tokens.HistoryIsReported ? T("approx. {0} of {1} tokens") : T("{0} of {1} tokens"), counted, window);
            return this.Budget switch
            {
                EmbeddedChatTokenBudgetState.FULL => $"{budget}. {this.FullContextText}",
                EmbeddedChatTokenBudgetState.WARNING => $"{budget}. {this.NearlyFullContextText}",
                _ => budget
            };
        }
    }

    /// <summary>
    /// Gets what the user can do once the conversation fills the context of the model.
    /// </summary>
    private string FullContextText
    {
        get
        {
            if (this.FullContextHint is { } hint)
                return hint;

            if (this.OffersCompression)
                return TA("This conversation fills the context window of the model. When you send your next message, AI Studio offers to compress the conversation.");

            return this.CanReset
                ? TA("This conversation fills the context window of the model. Start it over, or choose a provider whose model reads more at once.")
                : TA("This conversation fills the context window of the model. Choose a provider whose model reads more at once.");
        }
    }

    /// <summary>
    /// Gets the warning once the conversation fills most of the context of the model.
    /// </summary>
    private string NearlyFullContextText
    {
        get
        {
            if (this.OffersCompression)
                return T("This conversation fills most of the context window of the model. Once it is nearly full, AI Studio offers to compress it.");

            return this.CanReset
                ? TA("This conversation fills most of the context window of the model. Soon, you will have to start it over.")
                : T("This conversation fills most of the context window of the model.");
        }
    }

    /// <summary>
    /// Gets the classes of the message field; like on the chat page, they colour its helper text with the tokens.
    /// </summary>
    private string InputClass => this.Budget switch
    {
        EmbeddedChatTokenBudgetState.FULL => "mt-2 token-budget-exceeded",
        EmbeddedChatTokenBudgetState.WARNING => "mt-2 token-budget-nearly-full",
        _ => "mt-2"
    };

    private Task AttachmentsChanged(HashSet<FileAttachment> changedAttachments)
    {
        this.attachments = changedAttachments;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends the message of the user, with its attachments.
    /// </summary>
    private async Task SendAsync()
    {
        if (!this.CanSendMessage)
            return;

        var message = new ContentText { Text = this.userInput.Trim(), FileAttachments = [.. this.attachments] };
        if (!await this.ConfirmContextWindowAsync(message.Text) || !this.CanSend)
            return;

        this.userInput = string.Empty;
        this.attachments = [];
        await this.ChatService.SendAsync(this.ChatId, this.RequestOf(message));
    }

    /// <summary>
    /// Sends the message of the button, see QuickMessage, as if the user had written it. What the user typed meanwhile
    /// stays in the field.
    /// </summary>
    private async Task SendQuickMessageAsync()
    {
        if (this.QuickMessage is not { } quickMessage || !this.CanSend)
            return;

        if (!await this.ConfirmContextWindowAsync(quickMessage.Message) || !this.CanSend)
            return;

        await this.ChatService.SendAsync(this.ChatId, this.RequestOf(new ContentText { Text = quickMessage.Message }));
    }

    /// <summary>
    /// Asks the user what to do when a message would nearly fill the context window of the model, and compresses the
    /// conversation when the user wants to.
    /// </summary>
    /// <remarks>
    /// The count of the conversation leaves out the message being written, see RecountAsync, so the message is counted
    /// on its own here. A message after which the next answer would no longer fit is not sent at all, see
    /// EmbeddedChatTokenBudget.NoticeBeforeSending. Where the chat hides the context window, see ShowsContextWindow,
    /// that is all it says: the dialog names the window, which would tell apart models the user must not know.
    /// Compressing is offered where the chat allows it and where it makes room for the message; otherwise, the dialog
    /// says why it would not help, see ChatCompressionService.OfferBeforeSendingAsync. The message stays in the field
    /// then, and the user sends it afterward.
    /// </remarks>
    /// <param name="message">The text of the message about to be sent.</param>
    /// <returns>True when the message is to be sent.</returns>
    private async Task<bool> ConfirmContextWindowAsync(string message)
    {
        var options = ChatCompressionOptions.Of(this.SettingsManager.ConfigurationData.ChatCompression);
        if (EmbeddedChatTokenBudget.Of(this.tokens, options.ShareToAsk) is EmbeddedChatTokenBudgetState.UNKNOWN || this.conversation?.Thread is not { } thread)
            return true;

        var tokensWithMessage = this.tokens with { DraftTokens = await this.CountMessageAsync(message) };
        var notice = EmbeddedChatTokenBudget.NoticeBeforeSending(tokensWithMessage, this.ShowsContextWindow, options.ShareToAsk);
        if (notice is EmbeddedChatSendNotice.NONE)
            return true;

        if (!this.ShowsContextWindow)
        {
            await this.MessageBus.SendWarning(new(AppIcons.CONTEXT_WINDOW, TA("Together with this conversation, your message no longer fits into the context window of the model. Shorten your message, or remove some of its files.")));
            return false;
        }

        var offer = this.AllowsCompression && thread.IsLLMProviderAllowed(this.Provider)
            ? await this.CompressionService.OfferBeforeSendingAsync(thread, 0, this.Provider, tokensWithMessage, options, this.countToken)
            : ChatCompressionOffer.NONE;

        switch (await ContextWindowFullDialog.ShowAsync(this.DialogService, tokensWithMessage, ContextWindowFullSubject.EMBEDDED_CHAT, offer, canSendAnyway: notice is EmbeddedChatSendNotice.NEARLY_FULL))
        {
            case ContextWindowFullDecision.SEND_ANYWAY:
                return true;

            case ContextWindowFullDecision.COMPRESS:
                await this.CompressAsync();
                return false;

            default:
                return false;
        }
    }

    /// <summary>
    /// Counts the tokens of a message about to be sent.
    /// </summary>
    /// <param name="message">The text of the message.</param>
    /// <returns>The tokens; 0 when they could not be counted, so that the notice judges by the conversation alone.</returns>
    private async Task<int> CountMessageAsync(string message)
    {
        try
        {
            return await this.TokenCounter.CountTextAsync(this.Provider, message, this.countToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            this.Logger.LogWarning(exception, "Could not count the tokens of a message of the embedded chat '{ChatId}'.", this.ChatId);
            return 0;
        }
    }

    /// <summary>
    /// Compresses the conversation: the AI summarizes it, and the summary takes the place of the earlier messages.
    /// </summary>
    /// <remarks>
    /// The chat lives in a store of its assistant rather than in a workspace, so there is no place to keep the earlier
    /// messages: they give way to the summary, which the dialog says beforehand, see ChatCompressionTarget.SAME_CHAT.
    /// The last message of the user and the last answer stay as they are. The summary passes the prompt injection
    /// filter before it becomes a message, since the model reads it again. The conversation is replaced only once the
    /// compressed one is stored, see EmbeddedChatService.ReplaceAsync.
    /// </remarks>
    private async Task CompressAsync()
    {
        if (this.conversation is not { Thread: { } thread, IsRunning: false })
            return;

        // The message about to be sent waits in the field, so an unanswered last message is summarized with the rest:
        var plan = ChatCompressionPlan.Of(thread, 0, draftIsEmpty: false);
        if (plan is null)
            return;

        var dialogParameters = new DialogParameters<ChatCompressionDialog>
        {
            { x => x.SourceChat, thread },
            { x => x.Plan, plan },
            { x => x.ProviderSettings, this.Provider },
            { x => x.Target, ChatCompressionTarget.SAME_CHAT },
        };

        var dialogReference = await this.DialogService.ShowAsync<ChatCompressionDialog>(T("Compress Conversation"), dialogParameters, DialogOptions.BLOCKING_FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled || dialogResult.Data is not ChatCompressionDialogResult result)
            return;

        this.isCompressing = true;
        try
        {
            var sourceName = ChatCompression.NameOf(thread.Name);
            var payload = await this.AssistantLaunchService.CreateTextPayloadAsync(result.Summary, writtenByModel: true, thread.CaptureRestrictions(), sourceName);
            var summaryMessage = ChatCompression.SummaryMessage(ChatCompressionTarget.SAME_CHAT, sourceName, payload.Text);
            var compressed = WorkspaceBehaviour.CloneChat(thread);
            ChatCompression.TurnIntoContinuation(compressed, 0, ChatCompressionTarget.SAME_CHAT, summaryMessage, payload.Restrictions, this.Provider.Id);
            if (!await this.ChatService.ReplaceAsync(this.ChatId, compressed))
                await this.MessageBus.SendWarning(new(AppIcons.COMPRESS_CHAT, T("The conversation was not compressed, because the AI answered meanwhile.")));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.Logger.LogError(exception, "Could not store the compressed embedded chat '{ChatId}'.", this.ChatId);
            await this.MessageBus.SendError(new(AppIcons.COMPRESS_CHAT, TA("AI Studio could not save the compressed conversation, so it stays as it was. Please try again.")));
        }
        finally
        {
            this.isCompressing = false;
            this.StateHasChanged();
        }
    }

    /// <summary>
    /// Lets the model open the conversation, see StartRequest.
    /// </summary>
    private async Task StartAsync()
    {
        if (this.CanSend && !string.IsNullOrWhiteSpace(this.StartRequest))
            await this.ChatService.SendAsync(this.ChatId, this.RequestOf(new ContentText { Text = this.StartRequest }) with { IsHidden = true });
    }

    /// <summary>
    /// Sends the opening message. When it cannot be sent, e.g., because the provider may not answer, the message waits
    /// in the field instead, so that the user can send it once the reason is gone.
    /// </summary>
    /// <param name="message">The opening message.</param>
    private async Task SendOpeningMessageAsync(ContentText message)
    {
        if (this.CanSend && await this.ChatService.SendAsync(this.ChatId, this.RequestOf(message)))
            return;

        await this.InvokeAsync(() =>
        {
            this.userInput = message.Text;
            this.attachments = [.. message.FileAttachments];
            this.StateHasChanged();
        });
    }

    /// <summary>
    /// Gets the request which sends a message with the provider and the preparation of this chat.
    /// </summary>
    /// <param name="message">The message.</param>
    private EmbeddedChatRequest RequestOf(ContentText message) => new()
    {
        Provider = this.Provider,
        Message = message,
        HidesModelIdentity = this.HidesModelIdentity,
        PrepareAsync = this.PrepareAsync,
    };

    /// <summary>
    /// Sends the message when the user releases the shortcut they chose for the chat.
    /// </summary>
    /// <param name="keyEvent">The key released.</param>
    private async Task InputKeyEventAsync(KeyboardEventArgs keyEvent)
    {
        if (this.SettingsManager.ConfigurationData.Chat.ShortcutSendBehavior.IsSendShortcut(keyEvent))
            await this.SendAsync();
    }

    /// <summary>
    /// Stops the answer the model writes.
    /// </summary>
    private Task CancelAsync() => this.ChatService.CancelAsync(this.ChatId);

    /// <summary>
    /// Deletes the conversation after asking, so that it starts over.
    /// </summary>
    private async Task ResetAsync()
    {
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { dialog => dialog.Message, this.ResetQuestion ?? TA("Delete this conversation and start it over?") }
        };

        var reference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Start over"), parameters, DialogOptions.FULLSCREEN);
        var result = await reference.Result;
        if (result is null || result.Canceled)
            return;

        try
        {
            await this.ChatService.ResetAsync(this.ChatId, this.CreateThread);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.Logger.LogError(exception, "Could not delete the embedded chat '{ChatId}'.", this.ChatId);
            await this.MessageBus.SendError(new(AppIcons.DELETE, TA("AI Studio could not delete this conversation. Please try again.")));
        }
    }
}