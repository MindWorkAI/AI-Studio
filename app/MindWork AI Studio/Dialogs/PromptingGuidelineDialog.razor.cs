using Microsoft.AspNetCore.Components;
using AIStudio.Components;

namespace AIStudio.Dialogs;

public partial class PromptingGuidelineDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public string GuidelineMarkdown { get; set; } = string.Empty;

    private void Close() => this.MudDialog.Cancel();
}
