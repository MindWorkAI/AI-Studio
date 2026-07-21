namespace AIStudio.Tools.Rust;

/// <summary>
/// Rich clipboard content with a plain-text fallback.
/// </summary>
/// <param name="PlainText">The text used by applications that do not accept HTML.</param>
/// <param name="HtmlText">The HTML used by rich-text applications.</param>
public readonly record struct RichClipboardContent(string PlainText, string HtmlText);