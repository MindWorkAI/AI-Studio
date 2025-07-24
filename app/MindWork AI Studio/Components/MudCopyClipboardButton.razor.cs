using AIStudio.Pages;
using AIStudio.Tools.PluginSystem;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class MudCopyClipboardButton : ComponentBase
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(About).Namespace, nameof(About));
    
    /// <summary>
    /// The string that will be copied when the button is clicked.
    /// </summary>
    [Parameter]
    public required string CopyableContent { get; set; }
    
    /// <summary>
    /// The tooltip that should be shown to the user.
    /// </summary>
    [Parameter]
    public string ToolTipMessage { get; set; } = TB("Copies the content to the clipboard");
    
    [Inject] 
    private IJSRuntime JsRuntime { get; set; } = null!;
    
    [Inject]
    private ISnackbar Snackbar { get; init; } = null!;

    private async Task CopyToClipboard(string text)
    {
        try
        {
            await this.JsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
            this.Snackbar.Add(TB("Successfully copied the content to your clipboard"), Severity.Success);
        }
        catch (Exception)
        {
            this.Snackbar.Add(TB("Failed to copy the content to your clipboard"), Severity.Error);
        }
    }
    
}