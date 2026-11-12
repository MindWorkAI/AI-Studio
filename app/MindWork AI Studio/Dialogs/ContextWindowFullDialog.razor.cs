using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

/// <summary>
/// Tells the user before sending that the chat fills the context window of the model, or no
/// longer fits into it, and offers to compress it.
/// </summary>
/// <remarks>
/// Only a notice: it compresses nothing by itself, and the user may send the message anyway. The
/// numbers are those of the token count under the input field, so the dialog never disagrees with
/// what the user sees there.
/// </remarks>
public partial class ContextWindowFullDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// How many tokens the chat takes together with the message, formatted for the reader.
    /// </summary>
    [Parameter]
    public string ChatTokens { get; set; } = string.Empty;

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
    /// Whether the chat can be compressed, e.g., not before the AI answered for the first time.
    /// </summary>
    [Parameter]
    public bool CanCompress { get; set; }

    private void Cancel() => this.MudDialog.Cancel();

    private void SendAnyway() => this.MudDialog.Close(DialogResult.Ok(ContextWindowFullDecision.SEND_ANYWAY));

    private void Compress() => this.MudDialog.Close(DialogResult.Ok(ContextWindowFullDecision.COMPRESS));
}