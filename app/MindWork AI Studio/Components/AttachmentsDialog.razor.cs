using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class AttachmentsDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;
    
    [Parameter]
    public HashSet<string> DocumentPaths { get; set; } = new();
    
    private void Cancel() => this.MudDialog.Cancel();
    
}