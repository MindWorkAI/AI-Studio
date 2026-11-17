using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Shows the notes of the BeyondFacts Lab, which the person confirms having read.
/// </summary>
public partial class BflNoticeDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// Gets or sets the notes, as Markdown.
    /// </summary>
    [Parameter]
    public string Notice { get; set; } = string.Empty;

    private void Confirm() => this.MudDialog.Close(DialogResult.Ok(true));
}