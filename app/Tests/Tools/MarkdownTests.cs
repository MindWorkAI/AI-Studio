using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks that an answer which opens a code fence without closing it ends where it ends.
/// </summary>
/// <remarks>
/// A fence without its counterpart runs to the end of the document, so whatever is appended below an
/// answer is read as code instead of as Markdown. The chat never shows this, because it renders the
/// answer and the sources below it as two texts. A document is one text, and there an answer which
/// ends in an open fence takes the source list with it into a grey box.
/// </remarks>
[TestFixture]
public sealed class MarkdownTests
{
    [Test]
    public void AnOpenFenceGetsItsCounterpart()
    {
        var answer = Lines("Here is the code:", string.Empty, "```csharp", "var answer = 42;");

        Assert.That(Markdown.CloseOpenCodeFence(answer), Is.EqualTo(Lines(answer, "```")), "The fence is closed with the same three backticks which opened it.");
    }

    [Test]
    public void ALongerFenceIsClosedAtItsOwnLength()
    {
        // Four backticks are what a model writes when the block itself holds Markdown with code in
        // it. The three backticks inside are content then, not the end of the block:
        var answer = Lines("````markdown", "```csharp", "var answer = 42;", "```");

        Assert.That(Markdown.CloseOpenCodeFence(answer), Is.EqualTo(Lines(answer, "````")), "Only a fence of at least the opening length closes the block.");
    }

    [Test]
    public void ATildeFenceIsClosedWithTildes()
    {
        var answer = Lines("~~~", "var answer = 42;");

        Assert.That(Markdown.CloseOpenCodeFence(answer), Is.EqualTo(Lines(answer, "~~~")), "A block opened with tildes cannot be closed with backticks.");
    }

    [Test]
    public void AClosedFenceIsLeftAlone()
    {
        var answer = Lines("Here is the code:", string.Empty, "```csharp", "var answer = 42;", "```");

        Assert.That(Markdown.CloseOpenCodeFence(answer), Is.EqualTo(answer), "The usual case: the model closed its block itself.");
    }

    [Test]
    public void TextWithoutAnyFenceIsLeftAlone()
    {
        const string ANSWER = "Nothing in this answer opens a code block.";

        Assert.That(Markdown.CloseOpenCodeFence(ANSWER), Is.EqualTo(ANSWER));
    }

    [Test]
    public void AnEmptyTextIsLeftAlone()
    {
        Assert.That(Markdown.CloseOpenCodeFence(string.Empty), Is.Empty);
    }

    private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines);
}