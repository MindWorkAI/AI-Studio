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

    [Test]
    public void TheGroupingIsWhatTheChatAndTheExportBothRead()
    {
        // Mixed on purpose, and with two sources of one origin, so neither the order of the groups
        // nor the order inside a group can come from the order of the input:
        IList<Source> sources =
        [
            new("Handbook", "https://example.org/handbook", SourceOrigin.RAG),
            new("Search result", "https://example.org/search", SourceOrigin.TOOL),
            new("Cited by the model", "https://example.org/cited", SourceOrigin.LLM),
            new("Second handbook", "https://example.org/handbook-2", SourceOrigin.RAG),
        ];

        var listed = sources.GroupSources().SelectMany(group => group.Sources).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(sources.GroupSources(), Has.Count.EqualTo(3), "Each of the three origins has a source, so each of them is a group.");
            Assert.That(listed.Select(numbered => numbered.Source.Title), Is.EqualTo(new[] { "Cited by the model", "Search result", "Handbook", "Second handbook" }), "What the AI cited comes first, then what the tools read, then what the data providers gave.");
            Assert.That(listed.Select(numbered => numbered.Number), Is.EqualTo(new[] { 1, 2, 3, 4 }), "The number runs through the whole list instead of starting over per group.");
        });
    }

    [Test]
    public void AnOriginWithoutSourcesIsNoGroup()
    {
        IList<Source> sources = [new("Search result", "https://example.org/search", SourceOrigin.TOOL)];

        Assert.Multiple(() =>
        {
            Assert.That(sources.GroupSources().Select(group => group.Sources.Count), Is.EqualTo(new[] { 1 }), "An answer which only used a tool gets one group, not three with two of them empty.");
            Assert.That(new List<Source>().GroupSources(), Is.Empty, "An answer nobody had to look up gets no group at all.");
        });
    }

    [Test]
    public void TheMarkdownListsExactlyWhatTheGroupingSaysItShould()
    {
        IList<Source> sources =
        [
            new("Handbook (Page 12)", "file:///Users/someone/handbook.pdf#page=12", SourceOrigin.RAG),
            new("Cited by the model", "https://example.org/cited", SourceOrigin.LLM),
        ];

        var entries = EntriesOf(sources.ToMarkdown());
        var listed = sources.GroupSources().SelectMany(group => group.Sources).ToList();

        Assert.That(entries, Has.Count.EqualTo(listed.Count), "Every source the grouping lists is written out, and nothing else is.");
        for (var index = 0; index < entries.Count; index++)
            Assert.That(entries[index], Does.StartWith($"- [{listed[index].Number}] ").And.Contains(listed[index].Source.Title), "The Markdown and the chat read the same grouping, so a source cannot be numbered one way here and another way there.");
    }

    [Test]
    public void AReaderWhichCannotFollowAPageGetsTheDocumentWithoutOne()
    {
        IList<Source> sources =
        [
            new("Handbook (Page 266)", "file:///Users/someone/My Documents/handbook.pdf#page=266", SourceOrigin.RAG),
            new("An older answer", "file:///Users/someone/handbook.pdf#chunk=3", SourceOrigin.RAG),
            new("A section of an article", "https://example.org/article#results", SourceOrigin.LLM),
        ];

        Assert.That(EntriesOf(sources.ToMarkdown(keepPageAnchors: false)), Is.EqualTo(new[]
        {
            "- [1] [A section of an article](<https://example.org/article#results>)",
            "- [2] [Handbook (Page 266)](<file:///Users/someone/My%20Documents/handbook.pdf>)",
            "- [3] [An older answer](<file:///Users/someone/handbook.pdf>)",
        }), "Word and LibreOffice take the fragment of a local link for part of the file name and refuse the link, so the local links lose it -- and the web link keeps its own, where a fragment names a section of the page and belongs to the address.");
    }

    [Test]
    public void AReaderWhichFollowsAPageIsToldIt()
    {
        IList<Source> sources = [new("Handbook (Page 266)", "file:///Users/someone/handbook.pdf#page=266", SourceOrigin.RAG)];

        Assert.Multiple(() =>
        {
            Assert.That(EntriesOf(sources.ToMarkdown()).Single(), Does.EndWith("handbook.pdf#page=266>)"), "A browser and a PDF reader open the document where the passage is, so they are told the page.");
            Assert.That(EntriesOf(sources.ToExportMarkdown()).Single(), Does.EndWith("handbook.pdf#page=266>)"), "The clipboard and every text format keep it as well; only the two office formats ask for it to be dropped.");
        });
    }

    [Test]
    public void AKnownPageRidesInTheLinkOfASource()
    {
        var location = LocationOf("file:///Users/someone/My%20Documents/Gr%C3%B6%C3%9Fere%20%C3%9Cbersicht.pdf#page=12");

        Assert.Multiple(() =>
        {
            Assert.That(location.Path, Does.EndWith("Größere Übersicht.pdf").And.Contains("My Documents"), "The percent-encoding of the link is undone, so the program is handed the name the file really has.");
            Assert.That(location.PageNumber, Is.EqualTo(12), "This is the page the passage was found on, and the page the document is opened at.");
        });
    }

    [Test]
    public void APathOfAWindowsMachineComesBackAsOne()
    {
        var location = LocationOf("file:///C:/Users/someone/Documents/handbook.pdf#page=3");

        Assert.Multiple(() =>
        {
            Assert.That(location.Path, Is.EqualTo(@"C:\Users\someone\Documents\handbook.pdf"), "A drive letter and backslashes are what a program on Windows is handed -- and what the link was made from there.");
            Assert.That(location.PageNumber, Is.EqualTo(3));
        });
    }

    [Test]
    public void AChatFromBeforeThisReleaseKeepsItsDocumentAndLosesOnlyItsPage()
    {
        var location = LocationOf("file:///Users/someone/handbook.pdf#chunk=3");

        Assert.Multiple(() =>
        {
            Assert.That(location.Path, Does.EndWith("handbook.pdf"), "Such a source still names its document, so the click still opens it.");
            Assert.That(location.PageNumber, Is.Null, "A chunk is not a page: no program can be sent to one, so the document opens on its first page.");
        });
    }

    [Test]
    public void ALinkWithoutAFragmentNamesNoPage()
    {
        Assert.That(LocationOf("file:///Users/someone/handbook.pdf").PageNumber, Is.Null);
    }

    [Test]
    public void APageWhichIsNoPageIsReadAsNone()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LocationOf("file:///Users/someone/handbook.pdf#page=0").PageNumber, Is.Null, "Pages are counted from one, so a zero is not a page.");
            Assert.That(LocationOf("file:///Users/someone/handbook.pdf#page=-2").PageNumber, Is.Null);
            Assert.That(LocationOf("file:///Users/someone/handbook.pdf#page=twelve").PageNumber, Is.Null);
            Assert.That(LocationOf("file:///Users/someone/handbook.pdf#chunk=3&page=12").PageNumber, Is.EqualTo(12), "A link which already carried a fragment gets the page appended with an ampersand, and it is found there too.");
        });
    }

    [Test]
    public void AWebSourceNamesNoDocumentAtAll()
    {
        // The fragment reads like a page on purpose: what decides is the scheme, not the fragment.
        ISource source = new Source("Article", "https://example.org/article#page=12", SourceOrigin.LLM);

        Assert.That(source.TryGetDocumentLocation(out _), Is.False, "A web source is opened by the browser and has no path to hand to a program.");
    }

    /// <summary>
    /// Reads where the link of a source points, and fails the test when it points nowhere.
    /// </summary>
    /// <param name="url">The link of the source.</param>
    /// <returns>The document and the page the link names.</returns>
    private static SourceDocumentLocation LocationOf(string url)
    {
        ISource source = new Source("Handbook", url, SourceOrigin.RAG);

        Assert.That(source.TryGetDocumentLocation(out var location), Is.True, "This link names a file, so a location is what it has.");
        return location;
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