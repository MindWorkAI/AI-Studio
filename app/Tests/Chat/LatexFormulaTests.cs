using AIStudio.Chat;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks which code blocks of LaTeX the chat shows as a formula.
/// </summary>
[TestFixture]
public sealed class LatexFormulaTests
{
    [TestCase(@"\frac{a}{b}")]
    [TestCase(@"\iint_D f(x,y)\,\mathrm{d}x\,\mathrm{d}y")]
    [TestCase(@"\text{if } x > 0", Description = "Text inside a formula is part of it.")]
    [TestCase("a^2\n+ b^2\n= c^2", Description = "A formula may take several lines.")]
    [TestCase(@"\begin{pmatrix} 1 & 0 \\ 0 & 1 \end{pmatrix}")]
    [TestCase("\\begin{align}\n  a &= b \\\\\n  c &= d\n\\end{align}")]
    [TestCase(@"f(x) = \begin{cases} 1 & x > 0 \\ 0 & \text{otherwise} \end{cases}")]
    public void AFormulaIsAFormula(string latex)
    {
        Assert.That(LatexFormula.IsFormulaOnly(latex), Is.True);
    }

    [TestCase("\\documentclass{article}\n\\begin{document}\nHello\n\\end{document}", TestName = "A document")]
    [TestCase(@"\section{Results} The energy grows.", TestName = "A section")]
    [TestCase(@"\begin{itemize} \item One \end{itemize}", TestName = "A list")]
    [TestCase(@"\begin{tabular}{ll} a & b \end{tabular}", TestName = "A table")]
    [TestCase(@"\includegraphics{plot.png}", TestName = "A figure")]
    [TestCase("The value $x$ grows.", TestName = "Text with a formula in it")]
    [TestCase(@"The value \(x\) grows.", TestName = "Text with a formula in parentheses in it")]
    [TestCase("a^2 + b^2 = c^2\n\nE = mc^2", TestName = "Two formulas apart")]
    [TestCase(@"\begin{align", TestName = "An environment which never opens")]
    public void MoreThanAFormulaIsNoFormula(string latex)
    {
        Assert.That(LatexFormula.IsFormulaOnly(latex), Is.False, "MathJax would typeset it all as one formula.");
    }

    [TestCase("latex", "  E = mc^2  ", "E = mc^2")]
    [TestCase("tex", "E = mc^2", "E = mc^2")]
    [TestCase("LaTeX", "E = mc^2", "E = mc^2", Description = "The language is read in any case.")]
    [TestCase("latex", "$$\nE = mc^2\n$$", "E = mc^2", Description = "The markers inside the block are no part of the formula.")]
    [TestCase("latex", @"\[ E = mc^2 \]", "E = mc^2")]
    [TestCase("latex", @"\(E = mc^2\)", "E = mc^2")]
    [TestCase("latex", "$E = mc^2$", "E = mc^2")]
    [TestCase("math", @"\section{x}", @"\section{x}", Description = "A block of math holds a formula only, whatever it looks like.")]
    public void TheFormulaOfACodeBlockIsFound(string fenceLanguage, string code, string expected)
    {
        var found = LatexFormula.TryFind(fenceLanguage, code, out var formula);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(found, Is.True);
            Assert.That(found ? code[formula] : null, Is.EqualTo(expected));
        }
    }

    [TestCase("latex", "", TestName = "An empty block")]
    [TestCase("math", "$$ $$", TestName = "Nothing but markers")]
    [TestCase("latex", @"\section{Results}", TestName = "A section of a document")]
    [TestCase("python", "E = mc^2", TestName = "Code in another language")]
    [TestCase("", "E = mc^2", TestName = "A block without a language")]
    public void ACodeBlockWithoutAFormulaHasNone(string fenceLanguage, string code)
    {
        Assert.That(LatexFormula.TryFind(fenceLanguage, code, out _), Is.False);
    }
}