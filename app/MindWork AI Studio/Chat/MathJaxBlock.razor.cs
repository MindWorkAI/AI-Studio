using Microsoft.AspNetCore.Components;

namespace AIStudio.Chat;

/// <summary>
/// A formula of its own in the chat, which MathJax typesets, with a button to copy it and the actions
/// of its file, such as a button to save it.
/// </summary>
/// <remarks>
/// The buttons stand next to the element MathJax typesets, not inside it: MathJax replaces the content
/// of that element, see chat-math.js.
/// </remarks>
public partial class MathJaxBlock
{
    /// <summary>
    /// The formula, without the markers around it. The copy button copies it like this.
    /// </summary>
    [Parameter]
    public string Value { get; init; } = string.Empty;

    [Parameter]
    public string Class { get; init; } = string.Empty;

    /// <summary>
    /// The file of the formula, as PlainFileExport.ExtractFiles read it, or null when the formula
    /// cannot be saved, for example in a message whose code blocks cannot be saved either.
    /// </summary>
    [Parameter]
    public MessageFile? File { get; init; }

    /// <summary>
    /// What to show next to the copy button for the file, for example a button to save it.
    /// </summary>
    [Parameter]
    public RenderFragment<MessageFile>? Actions { get; init; }

    private string RootClass => string.IsNullOrWhiteSpace(this.Class)
        ? "formula-block"
        : $"formula-block {this.Class}";

    private string MathText => $"$${Environment.NewLine}{this.Value}{Environment.NewLine}$$";
}