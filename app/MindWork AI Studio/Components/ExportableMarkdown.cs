using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace AIStudio.Components;

/// <summary>
/// Renders Markdown the way MudMarkdown does, and shows actions next to every code block which is
/// one of the files of its message, such as a button to save that block.
/// </summary>
/// <remarks>
/// In a long answer, the export menu at the top of the message lies far away from the code block
/// the user is reading. MudMarkdown renders every code block through RenderCodeBlock, the highlighted
/// code together with its copy button, and lets us override that. We wrap what it renders and put our
/// actions beside it, so the copy button stays exactly as the library made it.
///
/// A block finds its file by where it starts. The chat renders an answer in parts, cut around its
/// formulas, and Markdig reads each part on its own: a block knows where it starts within its part
/// only. Added to where the part starts in the answer, that is the position the file was read with.
/// A block without a file, such as a formula or a block the model never closed, gets no actions,
/// just as the export menu does not offer it.
/// </remarks>
public sealed class ExportableMarkdown : MudMarkdown
{
    /// <summary>
    /// The files of the message, as PlainFileExport.ExtractFiles read them out of its text. Without
    /// any, this renders exactly as MudMarkdown does.
    /// </summary>
    [Parameter]
    public IReadOnlyList<MessageFile> Files { get; set; } = [];

    /// <summary>
    /// Where Value starts in the text the files were read from.
    /// </summary>
    [Parameter]
    public int ValueStart { get; set; }

    /// <summary>
    /// What to show next to the code block of a file, for example a button to save it.
    /// </summary>
    [Parameter]
    public RenderFragment<MessageFile>? CodeBlockActions { get; set; }

    /// <summary>
    /// Finds the file which starts at a position of the text the files were read from.
    /// </summary>
    /// <param name="files">The files of the message.</param>
    /// <param name="position">Where a block starts in the text of the message.</param>
    /// <returns>The file, or null when no file starts there.</returns>
    internal static MessageFile? FileAt(IReadOnlyList<MessageFile> files, int position) => files.FirstOrDefault(file => file.Position == position);

    #region Overrides of MudMarkdown

    /// <inheritdoc />
    protected override void RenderCodeBlock(in RenderTreeBuilder builder, ref int elementIndex, in Markdig.Syntax.CodeBlock code, in string? info)
    {
        if (this.CodeBlockActions is null || FileAt(this.Files, this.ValueStart + code.Span.Start) is not { } file)
        {
            base.RenderCodeBlock(builder, ref elementIndex, code, info);
            return;
        }

        //
        // Our frames are numbered by their place in this method, as Blazor asks for. The frames of
        // the library keep the running count it hands through every render method instead.
        //
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "code-block-with-actions");
        base.RenderCodeBlock(builder, ref elementIndex, code, info);

        builder.OpenElement(2, "div");
        builder.AddAttribute(3, "class", "code-block-actions");
        builder.AddContent(4, this.CodeBlockActions(file));
        builder.CloseElement();

        builder.CloseElement();
    }

    #endregion
}