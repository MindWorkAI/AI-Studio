using AIStudio.Tools;

using Microsoft.JSInterop;

using MudBlazor;

// ReSharper disable ClassNeverInstantiated.Global

namespace AIStudio;

/// <summary>
/// Wire up the clipboard service to copy markdown to the clipboard.
/// We use our own Rust-based clipboard service for this.
/// </summary>
public sealed class MarkdownClipboardService(Rust rust, IJSRuntime jsRuntime, ISnackbar snackbar) : IMudMarkdownClipboardService
{
    private IJSRuntime JsRuntime { get; } = jsRuntime;
    
    private ISnackbar Snackbar { get; } = snackbar;
    
    private Rust Rust { get; } = rust;

    /// <summary>
    /// Gets called when the user wants to copy the markdown to the clipboard.
    /// </summary>
    /// <param name="text">The Markdown text to copy.</param>
    public async ValueTask CopyToClipboardAsync(string text) => await this.Rust.CopyText2Clipboard(this.JsRuntime, this.Snackbar, text);
}