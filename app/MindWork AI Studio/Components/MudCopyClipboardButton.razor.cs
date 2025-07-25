using AIStudio.Chat;
using AIStudio.Pages;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class MudCopyClipboardButton : ComponentBase
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(About).Namespace, nameof(About));

    /// <summary>
    /// The string, if you want to copy a string.
    /// </summary>
    [Parameter]
    public string StringContent { get; set; } = string.Empty;
    
    /// <summary>
    /// The content, if you want to copy content.
    /// </summary>
    [Parameter]
    public IContent? Content { get; init; } 
    
    /// <summary>
    /// The content type, if you want to copy Content.
    /// </summary>
    [Parameter]
    public ContentType Type { get; init; } = ContentType.NONE;
    
    /// <summary>
    /// The tooltip that should be shown to the user.
    /// </summary>
    [Parameter]
    public string ToolTipMessage { get; set; } = TB("Copies the content to the clipboard");
    
    [Inject]
    private ISnackbar Snackbar { get; init; } = null!;
    
    [Inject]
    private RustService RustService { get; init; } = null!;
    
    private async Task HandleCopyClick()
    {
        if (this.Type == ContentType.NONE)
        {
            await this.CopyToClipboard(this.StringContent);
        }
        else
        {
            await this.CopyToClipboard(this.Content!);
        }
    }
    
    /// <summary>
    /// Copy this the string to the clipboard.
    /// </summary>
    private async Task CopyToClipboard(string textContent)
    {
        await this.RustService.CopyText2Clipboard(this.Snackbar, textContent);
    }
    
    /// <summary>
    /// Copy this block's content to the clipboard.
    /// </summary>
    private async Task CopyToClipboard(IContent contentToCopy)
    {
        switch (this.Type)
        {
            case ContentType.TEXT:
                var textContent = (ContentText) contentToCopy;
                await this.RustService.CopyText2Clipboard(this.Snackbar, textContent.Text);
                break;
            
            default:
                this.Snackbar.Add(TB("Cannot copy this content type to clipboard!"), Severity.Error, config =>
                {
                    config.Icon = Icons.Material.Filled.ContentCopy;
                    config.IconSize = Size.Large;
                    config.IconColor = Color.Error;
                });
                break;
        }
    }
    
}