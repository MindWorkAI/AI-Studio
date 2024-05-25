namespace AIStudio.Tools;

/// <summary>
/// Calling Rust functions.
/// </summary>
public sealed class Rust
{
    /// <summary>
    /// Tries to copy the given text to the clipboard.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime to access the Rust code.</param>
    /// <param name="snackbar">The snackbar to show the result.</param>
    /// <param name="text">The text to copy to the clipboard.</param>
    public async Task CopyText2Clipboard(IJSRuntime jsRuntime, ISnackbar snackbar, string text)
    {
        var response = await jsRuntime.InvokeAsync<SetClipboardResponse>("window.__TAURI__.invoke", "set_clipboard", new SetClipboardText(text));
        var msg = response.Success switch
        {
            true => "Successfully copied text to clipboard!",
            false => $"Failed to copy text to clipboard: {response.Issue}",
        };
                
        var severity = response.Success switch
        {
            true => Severity.Success,
            false => Severity.Error,
        };
                
        snackbar.Add(msg, severity, config =>
        {
            config.Icon = Icons.Material.Filled.ContentCopy;
            config.IconSize = Size.Large;
            config.IconColor = severity switch
            {
                Severity.Success => Color.Success,
                Severity.Error => Color.Error,
                        
                _ => Color.Default,
            };
        });
    }
}