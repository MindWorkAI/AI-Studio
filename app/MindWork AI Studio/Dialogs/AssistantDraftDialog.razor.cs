using AIStudio.Components;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class AssistantDraftDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public string DraftMarkdown { get; set; } = string.Empty;

    private void Cancel() => this.MudDialog.Cancel();

    private void Confirm() => this.MudDialog.Close(DialogResult.Ok(this.DraftMarkdown));

    private CodeBlockTheme CodeColorPalette => this.SettingsManager.IsDarkMode ? CodeBlockTheme.Dark : CodeBlockTheme.Default;

    private MudMarkdownStyling MarkdownStyling => new()
    {
        CodeBlock = { Theme = this.CodeColorPalette },
    };
}
