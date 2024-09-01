// ReSharper disable ClassNeverInstantiated.Global

namespace AIStudio.Tools.Services;

/// <summary>
/// Wire up the clipboard service to copy Markdown to the clipboard.
/// We use our own Rust-based clipboard service for this.
/// </summary>
public sealed class MarkdownClipboardService(RustService rust, ISnackbar snackbar) : IMudMarkdownClipboardService
{
    private ISnackbar Snackbar { get; } = snackbar;
    
    private RustService Rust { get; } = rust;

    /// <summary>
    /// Gets called when the user wants to copy the Markdown to the clipboard.
    /// </summary>
    /// <param name="text">The Markdown text to copy.</param>
    public async ValueTask CopyToClipboardAsync(string text) => await this.Rust.CopyText2Clipboard(this.Snackbar, text);
}