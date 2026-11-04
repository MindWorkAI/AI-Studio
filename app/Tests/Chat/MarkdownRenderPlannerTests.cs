using AIStudio.Chat;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks how the chat cuts a message into the parts it renders: Markdown, and the formulas between them.
/// </summary>
[TestFixture]
public sealed class MarkdownRenderPlannerTests
{
    [Test]
    public void ATextWithoutFormulasStaysOnePart()
    {
        Assert.That(Parts("Hello, **world**."), Is.EqualTo(new[] { "MARKDOWN: Hello, **world**." }));
    }

    [TestCase("$$", "$$")]
    [TestCase(@"\[", @"\]")]
    public void AFormulaOnLinesOfItsOwnBecomesAPartOfItsOwn(string openingMarker, string closingMarker)
    {
        var parts = Parts($"The energy follows from\n{openingMarker}\nE = mc^2\n{closingMarker}\nfor every body at rest.");

        Assert.That(parts, Is.EqualTo(new[] { "MARKDOWN: The energy follows from\n", "MATH_BLOCK: E = mc^2", "MARKDOWN: for every body at rest." }));
    }

    [Test]
    public void AFormulaInAListIsFoundAsWell()
    {
        var parts = Parts("1. The energy:\n   $$\n   E = mc^2\n   $$\n2. The mass");

        Assert.That(parts, Is.EqualTo(new[] { "MARKDOWN: 1. The energy:\n", "MATH_BLOCK:    E = mc^2", "MARKDOWN: 2. The mass" }));
    }

    [TestCase("```")]
    [TestCase("~~~")]
    public void AFormulaInACodeBlockIsCode(string fence)
    {
        var text = $"Write it like this:\n{fence}\n$$\nE = mc^2\n$$\n{fence}";

        Assert.That(Parts(text), Is.EqualTo(new[] { $"MARKDOWN: {text}" }));
    }

    [TestCase("```latex")]
    [TestCase("```tex")]
    [TestCase("```math")]
    [TestCase("```LaTeX")]
    [TestCase("``` latex")]
    [TestCase("~~~latex")]
    public void ACodeBlockOfLatexWithNothingButAFormulaBecomesAFormula(string openingFence)
    {
        var parts = Parts($"The integral:\n{openingFence}\n\\iint_D f(x,y)\\,\\mathrm{{d}}x\\,\\mathrm{{d}}y\n{openingFence[..3]}\nover the area D.");

        Assert.That(parts, Is.EqualTo(new[] { "MARKDOWN: The integral:\n", @"MATH_BLOCK: \iint_D f(x,y)\,\mathrm{d}x\,\mathrm{d}y", "MARKDOWN: over the area D." }));
    }

    [Test]
    public void TheMarkersAroundAFormulaInACodeBlockOfLatexAreNoPartOfIt()
    {
        Assert.That(Parts("```latex\n$$\nE = mc^2\n$$\n```"), Is.EqualTo(new[] { "MATH_BLOCK: E = mc^2" }));
    }

    [TestCase("```latex\n\\documentclass{article}\n\\begin{document}\nHello\n\\end{document}\n```", TestName = "A document")]
    [TestCase("```latex\nThe value $x$ grows.\n```", TestName = "Text with a formula in it")]
    [TestCase("```latex\n```", TestName = "An empty block")]
    [TestCase("```python\nE = mc^2\n```", TestName = "Code in another language")]
    [TestCase("```latex\nE = mc^2", TestName = "A block the model never closed")]
    public void ACodeBlockOfLatexWithMoreThanAFormulaStaysCode(string text)
    {
        Assert.That(Parts(text), Is.EqualTo(new[] { $"MARKDOWN: {text}" }));
    }

    [Test]
    public void EveryFormulaKnowsWhereItsBlockStarts()
    {
        var text = "First:\n$$\na = b\n$$\nSecond:\n\\[\nc = d\n\\]\nThird:\n```latex\ne = f\n```\nFourth:\n1. Item\n   $$\n   g = h\n   $$";

        var formulas = MarkdownRenderPlanner.Build(text).Segments.Where(segment => segment.Type is MarkdownRenderSegmentType.MATH_BLOCK).Select(segment => segment.FileStart);

        Assert.That(formulas, Is.EqualTo(new[] { text.IndexOf("$$", StringComparison.Ordinal), text.IndexOf(@"\[", StringComparison.Ordinal), text.IndexOf("```latex", StringComparison.Ordinal), text.IndexOf("   $$", StringComparison.Ordinal) }), "A formula starts with its markers, an indented one with the line of its marker.");
    }

    [Test]
    public void MarkdownStartsWhereItsBlockStarts()
    {
        var plan = MarkdownRenderPlanner.Build("Before\n$$\na = b\n$$\nAfter");

        Assert.That(plan.Segments.Where(segment => segment.Type is MarkdownRenderSegmentType.MARKDOWN).Select(segment => segment.FileStart - segment.Start), Is.All.Zero);
    }

    [Test]
    public void AFormulaTheModelNeverClosedLeavesEverythingAsMarkdown()
    {
        var text = "The first formula:\n$$\na^2 + b^2 = c^2\n$$\nThe second one broke off:\n$$\nE = mc^2";

        Assert.That(Parts(text), Is.EqualTo(new[] { $"MARKDOWN: {text}" }));
    }

    [Test]
    public void AnAnswerOfRawHtmlBecomesACodeBlock()
    {
        var parts = Parts("<html><body><p>Hi</p></body></html>");

        Assert.That(parts, Is.EqualTo(new[] { $"MARKDOWN: ```html{Environment.NewLine}<html><body><p>Hi</p></body></html>{Environment.NewLine}```" }));
    }

    [Test]
    public void TheThinkingOfAModelIsNotShown()
    {
        Assert.That(Parts("<think>Let me see.</think>\n\nThe answer."), Is.EqualTo(new[] { "MARKDOWN: The answer." }));
    }

    [TestCase("")]
    [TestCase("  \n  ")]
    [TestCase("<think>Still thinking", Description = "A model which is still thinking shows nothing yet.")]
    public void NothingToShowHasNoParts(string text)
    {
        Assert.That(MarkdownRenderPlanner.Build(text), Is.SameAs(MarkdownRenderPlan.EMPTY));
    }

    private static IEnumerable<string> Parts(string text)
    {
        var plan = MarkdownRenderPlanner.Build(text);
        return plan.Segments.Select(segment => $"{segment.Type}: {segment.GetContent(plan.Source)}");
    }
}