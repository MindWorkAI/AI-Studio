using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class EmbeddingResultDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public string ResultText { get; set; } = string.Empty;

    [Parameter]
    public string ResultLabel { get; set; } = string.Empty;

    private string ResultLabelText => string.IsNullOrWhiteSpace(this.ResultLabel) ? T("Embedding Vector") : this.ResultLabel;

    private void Close() => this.MudDialog.Close();
}
