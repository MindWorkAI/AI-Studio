using System.Collections.Frozen;

namespace AIStudio.Chat;

/// <summary>
/// Finds the formula in a code block which a model marked as latex, tex, or math.
/// </summary>
/// <remarks>
/// Models often put a single formula into such a block instead of between $$. The chat shows it as a
/// formula then, as it shows one between $$. A block of LaTeX which holds more than a formula, such as
/// a document or text with formulas in it, stays code: MathJax would typeset its text as one formula,
/// without spaces and in italics. When in doubt, a block stays code, since code shows what the model
/// wrote, and a broken formula does not.
/// </remarks>
internal static class LatexFormula
{
    private const string LATEX_LANGUAGE = "latex";
    private const string TEX_LANGUAGE = "tex";

    /// <summary>
    /// The language behind the fence of a block which holds nothing but a formula, as GitHub reads it.
    /// </summary>
    private const string MATH_LANGUAGE = "math";

    private const string BEGIN_ENVIRONMENT = @"\begin{";

    /// <summary>
    /// Commands which belong to a document or to its text, never to a formula.
    /// </summary>
    private static readonly string[] TEXT_COMMANDS =
    [
        @"\documentclass",
        @"\usepackage",
        @"\chapter",
        @"\section",
        @"\subsection",
        @"\subsubsection",
        @"\paragraph",
        @"\title",
        @"\author",
        @"\maketitle",
        @"\input",
        @"\include",
        @"\bibliography",
        @"\item",
        @"\caption",
    ];

    /// <summary>
    /// The environments of formulas which MathJax typesets. Any other environment, such as document,
    /// itemize, or tabular, belongs to a document.
    /// </summary>
    private static readonly FrozenSet<string> MATH_ENVIRONMENTS = FrozenSet.Create(StringComparer.Ordinal,
        "align", "align*", "alignat", "alignat*", "aligned", "alignedat", "array", "Bmatrix", "bmatrix", "cases", "CD",
        "eqnarray", "eqnarray*", "equation", "equation*", "flalign", "flalign*", "gather", "gather*", "gathered", "matrix",
        "multline", "multline*", "pmatrix", "smallmatrix", "split", "subarray", "Vmatrix", "vmatrix");

    /// <summary>
    /// The markers a model sometimes puts around the formula inside the block, too.
    /// </summary>
    private static readonly Delimiters[] MATH_DELIMITERS =
    [
        new("$$", "$$"),
        new(@"\[", @"\]"),
        new(@"\(", @"\)"),
        new("$", "$"),
    ];

    /// <summary>
    /// Finds the formula in a code block.
    /// </summary>
    /// <param name="fenceLanguage">The language behind the opening fence, for example latex.</param>
    /// <param name="code">The content of the block.</param>
    /// <param name="formula">Where the formula stands in the content, without the markers around it.</param>
    /// <returns>True when the block holds nothing but a formula.</returns>
    public static bool TryFind(ReadOnlySpan<char> fenceLanguage, ReadOnlySpan<char> code, out Range formula)
    {
        formula = default;
        var isMath = fenceLanguage.Equals(MATH_LANGUAGE, StringComparison.OrdinalIgnoreCase);
        if (!isMath && !fenceLanguage.Equals(LATEX_LANGUAGE, StringComparison.OrdinalIgnoreCase) && !fenceLanguage.Equals(TEX_LANGUAGE, StringComparison.OrdinalIgnoreCase))
            return false;

        var start = 0;
        var end = code.Length;
        Trim(code, ref start, ref end);
        foreach (var delimiters in MATH_DELIMITERS)
        {
            var text = code[start..end];
            if (text.Length < delimiters.Open.Length + delimiters.Close.Length || !text.StartsWith(delimiters.Open, StringComparison.Ordinal) || !text.EndsWith(delimiters.Close, StringComparison.Ordinal))
                continue;

            start += delimiters.Open.Length;
            end -= delimiters.Close.Length;
            Trim(code, ref start, ref end);
            break;
        }

        if (start >= end || (!isMath && !IsFormulaOnly(code[start..end])))
            return false;

        formula = start..end;
        return true;
    }

    /// <summary>
    /// Checks whether LaTeX is a formula and nothing else.
    /// </summary>
    /// <param name="latex">The LaTeX, without the markers around a formula.</param>
    /// <returns>True when MathJax can typeset it as one formula.</returns>
    internal static bool IsFormulaOnly(ReadOnlySpan<char> latex)
    {
        //
        // Markers inside mean text with formulas in it, and so does an empty line, which ends a paragraph:
        //
        if (latex.Contains('$') || latex.Contains(@"\(", StringComparison.Ordinal) || HasEmptyLine(latex))
            return false;

        foreach (var command in TEXT_COMMANDS)
            if (latex.Contains(command, StringComparison.Ordinal))
                return false;

        var environments = MATH_ENVIRONMENTS.GetAlternateLookup<ReadOnlySpan<char>>();
        var rest = latex;
        while (true)
        {
            var begin = rest.IndexOf(BEGIN_ENVIRONMENT, StringComparison.Ordinal);
            if (begin < 0)
                return true;

            rest = rest[(begin + BEGIN_ENVIRONMENT.Length)..];
            var nameEnd = rest.IndexOf('}');
            if (nameEnd < 0 || !environments.Contains(rest[..nameEnd]))
                return false;

            rest = rest[(nameEnd + 1)..];
        }
    }

    private static bool HasEmptyLine(ReadOnlySpan<char> latex)
    {
        foreach (var line in latex.EnumerateLines())
            if (line.IsWhiteSpace())
                return true;

        return false;
    }

    private static void Trim(ReadOnlySpan<char> text, ref int start, ref int end)
    {
        while (start < end && char.IsWhiteSpace(text[start]))
            start++;

        while (end > start && char.IsWhiteSpace(text[end - 1]))
            end--;
    }

    /// <summary>
    /// The markers before and after a formula.
    /// </summary>
    private sealed record Delimiters(string Open, string Close);
}