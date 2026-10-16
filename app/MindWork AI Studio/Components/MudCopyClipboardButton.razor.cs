using AIStudio.Chat;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class MudCopyClipboardButton : ComponentBase
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(MudCopyClipboardButton).Namespace, nameof(MudCopyClipboardButton));

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
    public string TooltipMessage { get; set; } = TB("Copies the content to the clipboard");

    /// <summary>
    /// Whether AI generated the content. It then ends with a notice saying so, when that is switched on.
    /// </summary>
    [Parameter]
    public bool IsAIGenerated { get; set; }

    /// <summary>
    /// The size of the button. The default size is small.
    /// </summary>
    [Parameter]
    public Size Size { get; set; } = Size.Small;

    [Inject]
    private RustService RustService { get; init; } = null!;

    [Inject]
    private AIGeneratedContentDisclosureService DisclosureService { get; init; } = null!;
    
    private async Task HandleCopyClick()
    {
        if (this.Type is ContentType.NONE)
            await this.CopyToClipboard(this.StringContent);
        else
            await this.CopyToClipboard(this.Content);
    }
    
    /// <summary>
    /// Copy this the string to the clipboard.
    /// </summary>
    private async Task CopyToClipboard(string textContent)
    {
        var text = this.IsAIGenerated ? this.DisclosureService.AddDisclosureToMarkdown(textContent) : textContent;
        await this.RustService.CopyText2Clipboard(text);
    }
    
    /// <summary>
    /// Copy this block's content to the clipboard.
    /// </summary>
    /// <remarks>
    /// The user copies what the card shows, and the card shows the answer together with the sources
    /// AI Studio collected for it. Pasting the answer into a mail without them would leave the
    /// reader with claims nobody is able to check.
    /// </remarks>
    private async Task CopyToClipboard(IContent? contentToCopy)
    {
        if (contentToCopy is null)
            return;

        switch (this.Type)
        {
            case ContentType.TEXT when contentToCopy.TryGetExportMarkdown(out var markdown):
                await this.CopyToClipboard(markdown);
                break;
            
            default:
                // This component is no MSGComponentBase, so it uses the shared bus instance the same
                // way FileExtensionValidation does:
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.ContentCopy, TB("Cannot copy this content type to clipboard.")));
                break;
        }
    }
    
}