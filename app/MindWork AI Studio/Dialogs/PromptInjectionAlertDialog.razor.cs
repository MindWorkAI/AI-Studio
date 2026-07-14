using AIStudio.Components;
using AIStudio.Tools.Security;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class PromptInjectionAlertDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public PromptInjectionScanResult Result { get; set; } = null!;

    private void Close() => this.MudDialog.Close();
}