using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

/// <summary>
/// A dialog that informs the user about something without asking for a decision. Use it when a
/// message must not be missed, e.g., when an action was refused.
/// </summary>
public partial class InformationDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// The message shown to the user.
    /// </summary>
    [Parameter]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The icon shown next to the message.
    /// </summary>
    [Parameter]
    public string Icon { get; set; } = Icons.Material.Filled.Info;

    /// <summary>
    /// The color of the icon.
    /// </summary>
    [Parameter]
    public Color IconColor { get; set; } = Color.Info;

    private void Close() => this.MudDialog.Close(DialogResult.Ok(true));
}