using AIStudio.Tools;

using Markdig.Syntax;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks how the list of sources reads once it is written out as Markdown.
/// </summary>
/// <remarks>
/// The sources are the one part of an answer which AI Studio writes itself, and since v26.9.1 they
/// no longer stay in the chat: they travel into every exported document and into the clipboard. A
/// number which starts over per group, or a title which breaks out of the link it sits in, is then
/// in a file somebody sends on. Nothing here asserts on the wording of a heading: I18N is
/// process-wide state without a reset, so an Init somewhere else would decide whether these pass.
/// </remarks>
[TestFixture]
public sealed class SourceExtensionsTests
{
    [Test]
    public void TheGroupsKeepTheirOrderAndTheNumbersRunThrough()
    {
        // Mixed on purpose, so that the order of the output cannot come from the order of the input:
        IList<Source> sources =
        [
            new("Handbook", "https://example.org/handbook", SourceOrigin.RAG),
            new("Search result", "https://example.org/search", SourceOrigin.TOOL),
            new("Cited by the model", "https://example.org/cited", SourceOrigin.LLM),
        ];

        Assert.That(EntriesOf(sources.ToMarkdown()), Is.EqualTo(new[]
        {
            "- [1] [Cited by the model](<https://example.org/cited>)",
            "- [2] [Search result](<https://example.org/search>)",
            "- [3] [Handbook](<https://example.org/handbook>)",
        }), "What the AI cited comes first, then what the tools read, then what the data providers gave -- and a reader can follow the numbers straight down the list.");
    }

    [Test]
    public void ATitleCannotBreakOutOfItsLink()
    {
        IList<Source> sources = [new("A [strange] title\\with a break\nin it", "https://example.org/", SourceOrigin.TOOL)];

        Assert.That(EntriesOf(sources.ToMarkdown()).Single(), Is.EqualTo(@"- [1] [A \[strange\] title\\with a break in it](<https://example.org/>)"), "Brackets and backslashes are escaped, and the line break becomes a space so the entry stays one line.");
    }

    [Test]
    public void ALocalPathKeepsWorkingAsALink()
    {
        // This is what a RAG hit on a file of the user looks like, and spaces in file names are the
        // rule rather than the exception:
        IList<Source> sources = [new("Handbook (page 12)", "file:///Users/someone/My Documents/handbook.pdf", SourceOrigin.RAG)];

        Assert.That(EntriesOf(sources.ToMarkdown()).Single(), Is.EqualTo("- [1] [Handbook (page 12)](<file:///Users/someone/My%20Documents/handbook.pdf>)"), "A space would end the link destination, so it is escaped.");
    }

    [Test]
    public void NoSourcesMeanNoText()
    {
        IList<Source> sources = [];

        Assert.Multiple(() =>
        {
            Assert.That(sources.ToMarkdown(), Is.Empty);
            Assert.That(sources.ToExportMarkdown(), Is.Empty, "An answer nobody had to look up gets no heading over an empty list.");
        });
    }

    [Test]
    public void TheExportPutsOneHeadingOfItsOwnAboveTheGroups()
    {
        IList<Source> sources =
        [
            new("Search result", "https://example.org/search", SourceOrigin.TOOL),
            new("Handbook", "https://example.org/handbook", SourceOrigin.RAG),
        ];

        var exported = sources.ToExportMarkdown();
        var document = Markdig.Markdown.Parse(exported, Markdown.SAFE_MARKDOWN_PIPELINE);

        Assert.Multiple(() =>
        {
            Assert.That(document.OfType<HeadingBlock>().Select(heading => heading.Level), Is.EqualTo(new[] { 1, 2, 2 }), "One heading of its own stands above the two groups the chat already shows.");
            Assert.That(exported, Does.EndWith(sources.ToMarkdown()), "Below that heading, the export is what the chat shows, unchanged.");
        });
    }

    /// <summary>
    /// Reads the entries of a source list, without the headings above them.
    /// </summary>
    /// <param name="markdown">The Markdown of the sources.</param>
    /// <returns>The entries, in the order they stand in.</returns>
    private static IReadOnlyList<string> EntriesOf(string markdown) => markdown
        .Split(Environment.NewLine, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Where(line => line.StartsWith("- [", StringComparison.Ordinal))
        .ToList();
}