using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Tools.PluginSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

/// <summary>
/// Tells the user before sending that the chat fills the context window of the model, or no
/// longer fits into it, and offers to compress it.
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
    /// Whether the chat can be compressed, e.g., not before the AI answered for the first time.
    /// </summary>
    [Parameter]
    public bool CanCompress { get; set; }

    /// <summary>
    /// Asks the user what to do with a message which fills the context window of the model.
    /// </summary>
    /// <remarks>
    /// The caller decides when to ask, see ConversationTokens.AsksBeforeSending, and whether
    /// compressing would help. The numbers are formatted with the culture of the active language.
    /// </remarks>
    /// <param name="dialogService">The dialog service of the caller.</param>
    /// <param name="tokens">What the conversation costs together with the message, with the context window of the model.</param>
    /// <param name="subject">What is about to be sent.</param>
    /// <param name="canCompress">Whether compressing is offered.</param>
    /// <returns>What the user decided; CANCEL when the dialog was closed.</returns>
    public static async Task<ContextWindowFullDecision> ShowAsync(IDialogService dialogService, ConversationTokens tokens, ContextWindowFullSubject subject, bool canCompress)
    {
        var isWindowExceeded = tokens.FillShare >= 1d;
        var culture = I18N.I.Culture;
        var dialogParameters = new DialogParameters<ContextWindowFullDialog>
        {
            { x => x.Tokens, TokenAmount.Format(tokens.Tokens, culture) },
            { x => x.WindowTokens, TokenAmount.Format(tokens.Window.DefaultTokens, culture) },
            { x => x.IsWindowExceeded, isWindowExceeded },
            { x => x.Subject, subject },
            { x => x.CanCompress, canCompress },
        };

        var title = isWindowExceeded ? TB("This chat is too long for the model") : TB("This chat nearly fills the context window of the model");
        var dialogReference = await dialogService.ShowAsync<ContextWindowFullDialog>(title, dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled || dialogResult.Data is not ContextWindowFullDecision decision)
            return ContextWindowFullDecision.CANCEL;

        return decision;
    }

    private void Cancel() => this.MudDialog.Cancel();

    private void SendAnyway() => this.MudDialog.Close(DialogResult.Ok(ContextWindowFullDecision.SEND_ANYWAY));

    private void Compress() => this.MudDialog.Close(DialogResult.Ok(ContextWindowFullDecision.COMPRESS));
}