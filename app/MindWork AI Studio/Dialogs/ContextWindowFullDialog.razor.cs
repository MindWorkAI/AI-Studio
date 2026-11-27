using AIStudio.AIOperations.ConversationSummary;
using AIStudio.Chat;
using AIStudio.Chat.Compression;
using AIStudio.Components;
using AIStudio.Tools.PluginSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

/// <summary>
/// Tells the user before sending that the chat or the request of an assistant fills the context
/// window of the model, or no longer fits into it, and offers to compress a chat.
/// </summary>
/// <remarks>
/// Only a notice: it compresses nothing by itself, and the user may send the message anyway. The
/// numbers are those of the token count under the input field, so the dialog never disagrees with
/// what the user sees there. Open it through ShowAsync, which every place that asks before sending
/// shares.
/// </remarks>
public partial class ContextWindowFullDialog : MSGComponentBase
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ContextWindowFullDialog).Namespace, nameof(ContextWindowFullDialog));

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// How many tokens the chat takes together with the message, formatted for the reader.
    /// </summary>
    [Parameter]
    public string Tokens { get; set; } = string.Empty;

    /// <summary>
    /// How many tokens the model reads at most, formatted for the reader.
    /// </summary>
    [Parameter]
    public string WindowTokens { get; set; } = string.Empty;

    /// <summary>
    /// Whether the chat takes more than the window, rather than nearly all of it.
    /// </summary>
    [Parameter]
    public bool IsWindowExceeded { get; set; }

    /// <summary>
    /// What is about to be sent, which decides the wording.
    /// </summary>
    [Parameter]
    public ContextWindowFullSubject Subject { get; set; } = ContextWindowFullSubject.CHAT;

    /// <summary>
    /// Whether compressing is offered when the dialog opens, or why it would not help, see ChatCompressionService.OfferBeforeSendingAsync.
    /// </summary>
    [Parameter]
    public ChatCompressionOffer Offer { get; set; } = ChatCompressionOffer.NONE;

    /// <summary>
    /// Whether the user may send the message anyway; not when the chat refuses a message which no longer fits.
    /// </summary>
    [Parameter]
    public bool CanSendAnyway { get; set; } = true;

    /// <summary>
    /// Where the conversation goes on when the user compresses, which decides the wording.
    /// </summary>
    [Parameter]
    public ChatCompressionTarget CompressionTarget { get; set; } = ChatCompressionTarget.NEW_CHAT;

    /// <summary>
    /// Decides again whether compressing is offered, with the settings as they are now; null where nothing compresses.
    /// </summary>
    /// <remarks>
    /// Called once the user closed the settings through the gear, since a model picked for compressing, a lower share,
    /// or a shorter summary may change the offer.
    /// </remarks>
    [Parameter]
    public Func<Task<ChatCompressionOffer>>? RecheckOffer { get; set; }

    private ChatCompressionOffer offer = ChatCompressionOffer.NONE;

    /// <summary>
    /// Asks the user what to do with a message which fills the context window of the model.
    /// </summary>
    /// <remarks>
    /// The caller decides when to ask, see ConversationTokens.AsksBeforeSending, and whether
    /// compressing would help, see ChatCompressionService.OfferBeforeSendingAsync. The numbers are
    /// formatted with the culture of the active language.
    /// </remarks>
    /// <param name="dialogService">The dialog service of the caller.</param>
    /// <param name="tokens">What the conversation costs together with the message, with the context window of the model.</param>
    /// <param name="subject">What is about to be sent.</param>
    /// <param name="offer">Whether compressing is offered, or why it would not help; NONE where nothing compresses.</param>
    /// <param name="canSendAnyway">Whether the user may send the message anyway, e.g., not where the chat refuses a message which no longer fits.</param>
    /// <param name="compressionTarget">Where the conversation goes on when the user compresses.</param>
    /// <param name="recheckOffer">Decides again whether compressing is offered, once the user changed the settings; null where nothing compresses.</param>
    /// <returns>What the user decided; CANCEL when the dialog was closed.</returns>
    public static async Task<ContextWindowFullDecision> ShowAsync(IDialogService dialogService, ConversationTokens tokens, ContextWindowFullSubject subject, ChatCompressionOffer offer, bool canSendAnyway = true, ChatCompressionTarget compressionTarget = ChatCompressionTarget.NEW_CHAT, Func<Task<ChatCompressionOffer>>? recheckOffer = null)
    {
        var isWindowExceeded = tokens.FillShare >= 1d;
        var culture = I18N.I.Culture;
        var dialogParameters = new DialogParameters<ContextWindowFullDialog>
        {
            { x => x.Tokens, TokenAmount.Format(tokens.Tokens, culture) },
            { x => x.WindowTokens, TokenAmount.Format(tokens.Window.DefaultTokens, culture) },
            { x => x.IsWindowExceeded, isWindowExceeded },
            { x => x.Subject, subject },
            { x => x.Offer, offer },
            { x => x.CanSendAnyway, canSendAnyway },
            { x => x.CompressionTarget, compressionTarget },
            { x => x.RecheckOffer, recheckOffer },
        };

        var title = subject is ContextWindowFullSubject.ASSISTANT_REQUEST
            ? isWindowExceeded ? TB("This request is too long for the model") : TB("This request nearly fills the context window of the model")
            : isWindowExceeded ? TB("This chat is too long for the model") : TB("This chat nearly fills the context window of the model");
        var dialogReference = await dialogService.ShowAsync<ContextWindowFullDialog>(title, dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled || dialogResult.Data is not ContextWindowFullDecision decision)
            return ContextWindowFullDecision.CANCEL;

        return decision;
    }

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.offer = this.Offer;
        await base.OnInitializedAsync();
    }

    #endregion

    /// <summary>
    /// Whether the dialog offers to compress, see Offer.
    /// </summary>
    private bool CanCompress => this.offer is ChatCompressionOffer.OFFERED;

    /// <summary>
    /// Why the dialog does not offer to compress, where the place compresses but it would not help; null otherwise.
    /// </summary>
    /// <remarks>
    /// The user guide recommends compressing a long chat, so a notice which leaves it out says why. In the chat of an
    /// assistant, the instructions of the assistant stay as well, and they are often the larger part.
    /// </remarks>
    private string? CompressionIssue => this.offer switch
    {
        ChatCompressionOffer.WINDOW_TOO_SMALL => string.Format(TA("Compressing is not possible, since it needs a model with a context window of at least {0} tokens. With the gear at the top, you can choose such a model for compressing."), ConversationSummaryBudget.MIN_CONTEXT_WINDOW.ToString("N0", I18N.I.Culture)),
        ChatCompressionOffer.MESSAGE_TOO_LONG => TA("Compressing would not help, since your message alone nearly fills the context window of the model."),
        ChatCompressionOffer.NOTHING_TO_SUMMARIZE => TA("Compressing would not help yet, since there is nothing to summarize before your last message and the last answer of the AI."),
        ChatCompressionOffer.KEEPS_TOO_MUCH when this.Subject is ContextWindowFullSubject.EMBEDDED_CHAT => TA("Compressing would not help, since the instructions of the assistant, your last message, and the last answer of the AI stay as they are, and together with your new message, they still nearly fill the context window of the model."),
        ChatCompressionOffer.KEEPS_TOO_MUCH when this.CompressionTarget is ChatCompressionTarget.SAME_CHAT => TA("Compressing would not help, since the system prompt, your last message with its files, and the last answer of the AI stay word for word, and together with your new message, they still nearly fill the context window of the model."),
        ChatCompressionOffer.KEEPS_TOO_MUCH => TA("Compressing would not help, since the new chat keeps the system prompt, your last message with its files, and the last answer of the AI word for word, and together with your new message, they still nearly fill the context window of the model."),
        _ => null,
    };

    /// <summary>
    /// Decides again whether compressing is offered, once the user closed the settings through the gear.
    /// </summary>
    private async Task ApplyChangedSettingsAsync()
    {
        if (this.RecheckOffer is not null)
            this.offer = await this.RecheckOffer();

        this.StateHasChanged();
    }

    private void Cancel() => this.MudDialog.Cancel();

    private void SendAnyway() => this.MudDialog.Close(DialogResult.Ok(ContextWindowFullDecision.SEND_ANYWAY));

    private void Compress() => this.MudDialog.Close(DialogResult.Ok(ContextWindowFullDecision.COMPRESS));
}