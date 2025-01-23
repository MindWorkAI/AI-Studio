using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class TextInfoLine : ComponentBase
{
    [Parameter]
    public string Label { get; set; } = string.Empty;

    [Parameter]
    public string Icon { get; set; } = Icons.Material.Filled.Info;

    [Parameter]
    public string Value { get; set; } = string.Empty;

    [Parameter]
    public string ClipboardTooltipSubject { get; set; } = "the text";

    [Parameter]
    public bool ShowingCopyButton { get; set; } = true;
    
    [Inject]
    private RustService RustService { get; init; } = null!;
    
    [Inject]
    private ISnackbar Snackbar { get; init; } = null!;
    
    private string ClipboardTooltip => $"Copy {this.ClipboardTooltipSubject} to the clipboard";
    
    private async Task CopyToClipboard(string content) => await this.RustService.CopyText2Clipboard(this.Snackbar, content);
}