using AIStudio.Tools.CodeHighlighting;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace AIStudio.Components;

/// <summary>
/// A code block in a language which AI Studio highlights itself, see CodeHighlighters. It is rendered
/// in the markup of MudBlazor.Markdown, so that it looks like the blocks the library highlights: the
/// same classes, the same colors of the theme, and the same copy button.
/// </summary>
/// <remarks>
/// Its parameters are strings only. Blazor then renders it again only when the code or the language
/// change, so while an answer streams in, only the block which grows is highlighted again.
/// </remarks>
public sealed class HighlightedCodeBlock : ComponentBase
{
    /// <summary>
    /// The code of the block.
    /// </summary>
    [Parameter]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// The language behind the opening fence, as the model wrote it, for example py.
    /// </summary>
    [Parameter]
    public string Language { get; set; } = string.Empty;

    #region Overrides of ComponentBase

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "hljs mud-markdown-code-highlight");

        builder.OpenComponent<CodeBlockCopyButton>(2);
        builder.AddComponentParameter(3, nameof(CodeBlockCopyButton.Text), this.Code);
        builder.CloseComponent();

        //
        // MudMarkdown leaves the typography of its code blocks at the default value, and so do we:
        //
        builder.OpenComponent<MudText>(4);
        builder.AddComponentParameter(5, nameof(MudText.HtmlTag), "pre");
        builder.AddComponentParameter(6, nameof(MudText.Typo), default(Typo));
        builder.AddComponentParameter(7, nameof(MudText.ChildContent), (RenderFragment)this.RenderCode);
        builder.CloseComponent();

        builder.CloseElement();
    }

    #endregion

    private void RenderCode(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "code");
        builder.AddAttribute(1, "class", $"hljs language-{this.Language}");
        if (CodeHighlighters.TryGetRules(this.Language, out var rules))
        {
            var position = 0;
            foreach (var token in CodeTokenizer.Tokenize(this.Code, rules))
            {
                if (token.Start > position)
                    builder.AddContent(2, this.Code[position..token.Start]);

                builder.OpenElement(3, "span");
                builder.AddAttribute(4, "class", token.Kind.CssClass);
                builder.AddContent(5, this.Code.Substring(token.Start, token.Length));
                builder.CloseElement();
                position = token.Start + token.Length;
            }

            if (position < this.Code.Length)
                builder.AddContent(6, this.Code[position..]);
        }
        else
            builder.AddContent(7, this.Code);

        builder.CloseElement();
    }
}