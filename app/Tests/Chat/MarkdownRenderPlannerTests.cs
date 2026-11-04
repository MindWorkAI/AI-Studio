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