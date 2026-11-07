using AIStudio.Chat;
using AIStudio.Chat.Compression;
using AIStudio.Components;

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
    private ChatCompressionService ChatCompressionService { get; init; } = null!;

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
    /// The provider which writes the summary: the one the chat would send its next message to.
    /// </summary>
    [Parameter]
    public AIStudio.Settings.Provider ProviderSettings { get; set; } = AIStudio.Settings.Provider.NONE;

    /// <summary>
    /// The name suggested for the new chat.
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
    private ChatCompressionProgress progress;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Configure the spellchecking for the user input:
        this.SettingsManager.InjectSpellchecking(USER_INPUT_ATTRIBUTES);
        this.chatName = this.SuggestedName;
        await base.OnInitializedAsync();
    }

    #endregion

    private bool HasAttachments => this.Plan.Preamble.Concat(this.Plan.ToSummarize).Any(block => block.Content?.FileAttachments.Count > 0);

    private string SummaryScope => this.Plan.ToSummarize.Count is 1
        ? string.Format(T("'{0}' summarizes 1 message."), this.ProviderSettings.InstanceName)
        : string.Format(T("'{0}' summarizes {1} messages."), this.ProviderSettings.InstanceName, this.Plan.ToSummarize.Count);

    private string ProgressMessage => this.progress.Parts > 1
        ? string.Format(T("The AI is summarizing part {0} of {1}..."), this.progress.Part, this.progress.Parts)
        : T("The AI is summarizing the chat...");

    private string? ValidateChatName(string? value) => string.IsNullOrWhiteSpace(value) ? T("Please enter a chat name.") : null;

    private async Task HandleChatNameKeyDown(KeyboardEventArgs keyEvent)
    {
        var key = keyEvent.Key.ToLowerInvariant();
        var code = keyEvent.Code.ToLowerInvariant();
        if (key is not "enter" && code is not "enter" and not "numpadenter")
            return;

        if (keyEvent is { AltKey: true } or { CtrlKey: true } or { MetaKey: true })
            return;

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

        var progressReporter = new Progress<ChatCompressionProgress>(reported =>
        {
            this.progress = reported;
            this.InvokeAsync(this.StateHasChanged).Observe($"{nameof(ChatCompressionDialog)}: showing the progress of the summary");
        });

        var result = await this.ChatCompressionService.SummarizeAsync(this.SourceChat, this.Plan, this.ProviderSettings, this.focus, progressReporter, this.cancellationTokenSource.Token);

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