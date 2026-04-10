using AIStudio.Components;
using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class MandatoryInfoDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public DataMandatoryInfo Info { get; set; } = new();

    [Parameter]
    public DataMandatoryInfoAcceptance? Acceptance { get; set; }

    private void Accept() => this.MudDialog.Close(DialogResult.Ok(true));

    private void Reject() => this.MudDialog.Close(DialogResult.Ok(false));
}