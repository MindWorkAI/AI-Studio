using AIStudio.Tools.CodeHighlighting;

using Microsoft.AspNetCore.Components.Rendering;

namespace AIStudio.Components;

/// <summary>
/// Renders Markdown the way MudMarkdown does, and highlights the code blocks of more languages than
/// the library does, such as Python or SQL.
/// </summary>
/// <remarks>
/// MudBlazor.Markdown highlights only a few languages itself, and its highlighter is internal.
/// MudMarkdown renders every code block through RenderCodeBlock, though, and lets us override that.
/// A block in one of the languages of CodeHighlighters becomes a HighlightedCodeBlock; any other block
/// stays as the library renders it.
/// </remarks>
public class HighlightedMarkdown : MudMarkdown
{
    /// <summary>
    /// The text of a code block: its lines, each with the line break it came with.
    /// </summary>
    /// <param name="code">The code block, as Markdig read it.</param>
    /// <returns>The text of the block.</returns>
    internal static string TextOf(Markdig.Syntax.CodeBlock code) => code.Lines.ToString();

    #region Overrides of MudMarkdown

    /// <inheritdoc />
    protected override void RenderCodeBlock(in RenderTreeBuilder builder, ref int elementIndex, in Markdig.Syntax.CodeBlock code, in string? info)
    {
        if (!CodeHighlighters.TryGetRules(info, out _))
        {
            //
            // The copy button of the library confirms in the language of the app, as ours does:
            //
            this.Styling.CodeBlock.CopyButtonText = CodeBlockCopyButton.ConfirmationText;
            base.RenderCodeBlock(builder, ref elementIndex, code, info);
            return;
        }

        //
        // Our frames are numbered by their place in this method, as Blazor asks for. The frames of
        // the library keep the running count it hands through every render method instead:
        //
        builder.OpenComponent<HighlightedCodeBlock>(0);
        builder.AddComponentParameter(1, nameof(HighlightedCodeBlock.Code), TextOf(code));
        builder.AddComponentParameter(2, nameof(HighlightedCodeBlock.Language), info);
        builder.CloseComponent();
    }

    #endregion
}