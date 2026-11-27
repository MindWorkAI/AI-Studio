using AIStudio.AIOperations;
using AIStudio.AIOperations.ConversationSummary;
using AIStudio.Chat;
using AIStudio.Chat.Compression;
using AIStudio.Components;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AIStudio.Dialogs;

/// <summary>
/// Asks the AI for a summary of a chat, for continuing the conversation in a new chat.
/// </summary>
/// <remarks>
/// The dialog only writes the summary. Creating the new chat and switching to it is left to the
/// chat, which knows what is on the screen, see ChatComponent.CompressCurrentChat. Canceling while
/// the AI writes stops the request, and nothing changes.
/// </remarks>
public partial class ChatCompressionDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Inject]
    private ConversationSummaryOperation ConversationSummary { get; init; } = null!;

    [Inject]
    private ChatCompressionService ChatCompressionService { get; init; } = null!;

    [Inject]
    private AIOperationProviderResolver ProviderResolver { get; init; } = null!;

    /// <summary>
    /// The chat to compress.
    /// </summary>
    [Parameter]
    public ChatThread SourceChat { get; set; } = null!;

    /// <summary>
    /// Which messages the summary replaces, see ChatCompressionPlan.Of.
    /// </summary>
    [Parameter]
    public ChatCompressionPlan Plan { get; set; } = null!;

    /// <summary>
    /// The provider of the chat: the one it would send its next message to, and which continues with the summary.
    /// </summary>
    /// <remarks>
    /// It writes the summary as well, unless the settings pick another model for compressing which may see the
    /// chat, see ChatCompressionService.GetProviderOptions.
    /// </remarks>
    [Parameter]
    public AIStudio.Settings.Provider ChatProvider { get; set; } = AIStudio.Settings.Provider.NONE;

    /// <summary>
    /// Where the conversation goes on: in a new chat, which the user names here, or in the same chat.
    /// </summary>
    [Parameter]
    public ChatCompressionTarget Target { get; set; } = ChatCompressionTarget.NEW_CHAT;

    /// <summary>
    /// The name suggested for the new chat; not asked for when the conversation goes on in the same chat.
    /// </summary>
    [Parameter]
    public string SuggestedName { get; set; } = string.Empty;

    /// <summary>
    /// Whether the chat has unsaved changes, which it stores before the new chat opens.
    /// </summary>
    [Parameter]
    public bool StoresChatFirst { get; set; }

    private static readonly Dictionary<string, object?> USER_INPUT_ATTRIBUTES = new();

    private readonly CancellationTokenSource cancellationTokenSource = new();

    private MudForm form = null!;
    private string chatName = string.Empty;
    private string focus = string.Empty;
    private string issue = string.Empty;
    private bool isSummarizing;
    private ConversationSummaryProgress progress;
    private ChatCompressionOptions options = ChatCompressionOptions.DEFAULT;
    private AIOperationProviderChoice writer = new(AIStudio.Settings.Provider.NONE, AIOperationProviderIssue.NOT_SET, AIOperationProviderIssue.NOT_SET);

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Configure the spellchecking for the user input:
        this.SettingsManager.InjectSpellchecking(USER_INPUT_ATTRIBUTES);
        this.chatName = this.SuggestedName;

        // Read once, so that the summary uses the settings the dialog opened with:
        this.options = ChatCompressionOptions.Of(this.SettingsManager.ConfigurationData.ChatCompression);
        this.focus = this.options.PreselectedFocus;
        this.writer = this.ChatCompressionService.ChooseWriter(this.SourceChat, this.ChatProvider);
        await base.OnInitializedAsync();
    }

    #endregion

    private bool HasDocumentsToSummarize => this.Plan.ToSummarize.Any(block => block.Content?.FileAttachments.MayContainDocuments() is true);

    private bool HasImagesToSummarize => this.Plan.ToSummarize.Any(block => block.Content?.FileAttachments.ContainsImages() is true);

    private string SummaryScope => this.Plan.ToSummarize.Count is 1
        ? string.Format(T("'{0}' summarizes 1 message."), this.writer.Provider.InstanceName)
        : string.Format(T("'{0}' summarizes {1} messages."), this.writer.Provider.InstanceName, this.Plan.ToSummarize.Count);

    /// <summary>
    /// Why the model of the chat writes the summary instead of the model picked for compressing, or an empty string when it does not.
    /// </summary>
    /// <remarks>
    /// Told from the reason the choice reports, never guessed afterward, see AIOperationProviderIssue. Without a model
    /// picked for compressing, the model of the chat writes the summary as intended, and there is nothing to tell.
    /// </remarks>
    private string FallbackNotice => !this.writer.UsesFallback ? string.Empty : this.writer.PreferredIssue switch
    {
        AIOperationProviderIssue.NOT_SET => string.Empty,
        AIOperationProviderIssue.MISSING => T("The model picked for compressing chats no longer exists, so the model of this chat writes the summary."),
        AIOperationProviderIssue.PROHIBITED => TA("Your organization does not allow the model picked for compressing chats, so the model of this chat writes the summary."),
        AIOperationProviderIssue.BELOW_MINIMUM_CONFIDENCE => T("The model picked for compressing chats does not reach the minimum confidence level, so the model of this chat writes the summary."),
        AIOperationProviderIssue.NOT_ALLOWED_FOR_CONTENT => T("This chat holds data which the model picked for compressing chats must not see, so the model of this chat writes the summary."),
        AIOperationProviderIssue.CONTEXT_WINDOW_TOO_SMALL => T("The context window of the model picked for compressing chats is too small, so the model of this chat writes the summary."),

        _ => T("The model picked for compressing chats cannot write this summary, so the model of this chat writes it."),
    };

    private string ProgressMessage => this.progress.Step switch
    {
        ConversationSummaryStep.SUMMARIZING when this.progress.Total > 1 => string.Format(T("The AI is summarizing part {0} of {1}..."), this.progress.Current, this.progress.Total),
        ConversationSummaryStep.SUMMARIZING => T("The AI is summarizing the chat..."),
        ConversationSummaryStep.CONDENSING => T("The summary grew too long. The AI is shortening it..."),

        _ => T("AI Studio is reading the messages and their files..."),
    };

    private string? ValidateChatName(string? value) => string.IsNullOrWhiteSpace(value) ? TA("Please enter a chat name.") : null;

    private async Task HandleChatNameKeyDown(KeyboardEventArgs keyEvent)
    {
        if (keyEvent.IsPlainEnter)
            await this.CompressAsync();
    }

    private async Task CompressAsync()
    {
        await this.form.ValidateAsync();
        if (!this.form.IsValid || this.isSummarizing)
            return;

        this.isSummarizing = true;
        this.issue = string.Empty;
        this.progress = default;
        await this.InvokeAsync(this.StateHasChanged);

        var progressReporter = new Progress<ConversationSummaryProgress>(reported =>
        {
            this.progress = reported;
            this.InvokeAsync(this.StateHasChanged).Observe($"{nameof(ChatCompressionDialog)}: showing the progress of the summary");
        });

        var request = new ConversationSummaryRequest(this.SourceChat, this.Plan, this.focus, this.options.MaxSummaryTokens, this.ChatProvider.GetModelProfile().Context, progressReporter);

        // Chosen again right before running, since the settings may have changed while the dialog was open:
        this.writer = this.ProviderResolver.Resolve(this.ConversationSummary, request, ConversationSummaryOperation.GetProviderSettings(this.SettingsManager), ChatCompressionService.GetProviderOptions(this.SourceChat, this.ChatProvider));
        if (!this.writer.HasProvider)
        {
            this.issue = string.Format(TA("No model may write the summary of this chat. Please select a model with a context window of at least {0} tokens."), ConversationSummaryBudget.MIN_CONTEXT_WINDOW.ToString("N0", I18N.I.Culture));
            this.isSummarizing = false;
            await this.InvokeAsync(this.StateHasChanged);
            return;
        }

        var result = await this.ConversationSummary.RunAsync(request, new AIOperationContext(this.writer.Provider), this.cancellationTokenSource.Token);

        // The user canceled, and the dialog is gone already:
        if (this.cancellationTokenSource.IsCancellationRequested)
            return;

        if (!result.Success)
        {
            this.issue = result.Issue;
            this.isSummarizing = false;
            await this.InvokeAsync(this.StateHasChanged);
            return;
        }

        this.MudDialog.Close(DialogResult.Ok(new ChatCompressionDialogResult(this.chatName.Trim(), result.Summary)));
    }

    private void Cancel()
    {
        this.cancellationTokenSource.Cancel();
        this.MudDialog.Cancel();
    }

    #region Overrides of MSGComponentBase

    protected override void DisposeResources()
    {
        this.cancellationTokenSource.Cancel();
        this.cancellationTokenSource.Dispose();
        base.DisposeResources();
    }

    #endregion
}