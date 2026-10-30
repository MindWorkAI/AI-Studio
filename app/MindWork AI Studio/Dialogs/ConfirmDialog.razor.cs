using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

/// <summary>
/// A confirmation dialog that can be used to ask the user for confirmation.
/// </summary>
public partial class ConfirmDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional Markdown content rendered instead of using the message property.
    /// </summary>
    [Parameter]
    public string MarkdownBody { get; set; } = string.Empty;

    /// <summary>
    /// The color of the confirming button. Red by default, since most confirmations ask before something is deleted.
    /// </summary>
    [Parameter]
    public Color ConfirmColor { get; set; } = Color.Error;

    private void Cancel() => this.MudDialog.Cancel();
    
    private void Confirm() => this.MudDialog.Close(DialogResult.Ok(true));
}