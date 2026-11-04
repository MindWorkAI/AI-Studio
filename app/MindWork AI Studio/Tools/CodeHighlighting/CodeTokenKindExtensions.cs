namespace AIStudio.Tools.CodeHighlighting;

public static class CodeTokenKindExtensions
{
    /// <param name="kind">What the part is.</param>
    extension(CodeTokenKind kind)
    {
        /// <summary>
        /// The CSS class which MudBlazor.Markdown gives this kind of part, so that it takes its color from
        /// the palette of the theme, just as the languages the library highlights itself.
        /// </summary>
        /// <returns>The CSS class of the part.</returns>
        public string ToCssClass() => kind switch
        {
            CodeTokenKind.KEYWORD => "hljs-keyword",
            CodeTokenKind.TYPE => "hljs-type",
            CodeTokenKind.LITERAL => "hljs-literal",
            CodeTokenKind.STRING => "hljs-string",
            CodeTokenKind.COMMENT => "hljs-comment",
            CodeTokenKind.NUMBER => "hljs-number",
            CodeTokenKind.FUNCTION => "hljs-title function_",
            CodeTokenKind.META => "hljs-meta",
            CodeTokenKind.VARIABLE => "hljs-variable",
            CodeTokenKind.ATTRIBUTE => "hljs-attr",
            CodeTokenKind.SELECTOR => "hljs-selector-class",

            _ => string.Empty,
        };
    }
}