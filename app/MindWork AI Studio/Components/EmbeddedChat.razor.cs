using AIStudio.Chat;
using AIStudio.Chat.Embedded;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AIStudio.Components;

/// <summary>
/// A chat which an assistant shows on its own page, such as one of the two chats of a task of the Model Comparison
/// assistant.
/// </summary>
/// <remarks>
/// The chat itself lives in EmbeddedChatService, so an answer goes on while the user looks elsewhere; this component
/// shows it and renders whenever the service says it changed. What a request carries beside the conversation, such as
/// the system prompt and the tools, the assistant decides through PrepareAsync. The count of tokens leaves out the
/// context window of the model on purpose: it would tell models apart which the user compares without knowing them.
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
    public Func<ChatThread, Task<bool>> PrepareAsync { get; set; } = _ => Task.FromResult(true);

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

    [Inject]
    private EmbeddedChatService ChatService { get; init; } = null!;

    [Inject]
    private ConversationTokenCounter TokenCounter { get; init; } = null!;

    /// <summary>
    /// Ends a count when the component goes away.
    /// </summary>
    private readonly CancellationTokenSource countCancellation = new();

    /// <summary>
    /// The token of countCancellation, taken while the source still lives, see BflChat.
    /// </summary>
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

    private bool IsRunning => this.conversation?.IsRunning ?? false;

    private bool CanSend => this.conversation is { IsAvailable: true, IsRunning: false } && this.ProviderIssue is null;

    private bool CanSendMessage => this.CanSend && !string.IsNullOrWhiteSpace(this.userInput);

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
        else if (this.countedProviderId != this.Provider.Id)
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
    /// The system prompt counts as the chat stores it: what PrepareAsync adds for a request is not known before. The
    /// count is therefore an estimate, and the message being written is left out.
    /// </remarks>
    private async Task RecountAsync()
    {
        this.countedProviderId = this.Provider.Id;
        if (this.IsReadOnly || this.conversation is not { Thread: { } thread, IsRunning: false } || this.ProviderIssue is not null || this.Provider.UsedLLMProvider is LLMProviders.NONE)
        {
            this.tokens = ConversationTokens.UNAVAILABLE;
            return;
        }

        try
        {
            var parts = ConversationParts.Of(thread, thread.SystemPrompt, string.Empty, null, false, null);
            this.tokens = await this.TokenCounter.CountAsync(this.Provider, parts, thread.ReportedHistoryFor(this.Provider.Model), this.countToken);
        }
        catch (OperationCanceledException)
        {
            // The component went away.
        }
    }

    /// <summary>
    /// Gets about how many tokens the conversation carries, or null when nothing could be counted.
    /// </summary>
    private string? TokenText => this.tokens.IsKnown
        ? string.Format(T("approx. {0} tokens"), TokenAmount.Format(this.tokens.Tokens, Tools.PluginSystem.I18N.I.Culture))
        : null;

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
        this.userInput = string.Empty;
        this.attachments = [];
        await this.ChatService.SendAsync(this.ChatId, this.RequestOf(message));
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
}