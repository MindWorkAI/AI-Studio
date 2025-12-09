using System.Formats.Asn1;
using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

/// <summary>
/// Check how your file will be loaded by Pandoc.
/// </summary>
public partial class PandocDocumentCheckDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;
    
    private string documentContent = string.Empty;
    
    private void Cancel() => this.MudDialog.Cancel();
}