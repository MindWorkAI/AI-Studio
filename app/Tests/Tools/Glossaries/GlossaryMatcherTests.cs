using System.Diagnostics;
using System.Text;

using AIStudio.Tools.Glossaries;

namespace AIStudio.Tests.Tools.Glossaries;

/// <summary>
/// Checks which entries of a glossary count as part of a text.
/// </summary>
/// <remarks>
/// A missing entry costs the user the exact translation or explanation they maintained, a needless one
/// only a few tokens. The matcher therefore finds a term inside a compound, across a line break, and in
/// any case, but leaves out terms so short that they would match almost every text.
/// </remarks>
[TestFixture]
public sealed class GlossaryMatcherTests
{
    [Test]
    public void ATermInsideACompoundMatches()
    {
        IReadOnlyList<GlossaryEntry> entries = [new("Arbeitszeit", "working time"), new("Urlaub", "vacation")];

        var found = GlossaryMatcher.For(entries).FindEntries("Die Arbeitszeitregelung gilt ab Montag.");

        Assert.That(found.Select(entry => entry.Term), Is.EqualTo(new[] { "Arbeitszeit" }));
    }

    [Test]
    public void ATermMatchesInAnyCaseAndAcrossALineBreak()
    {
        IReadOnlyList<GlossaryEntry> entries = [new("Data  Protection Officer", "Datenschutzbeauftragte:r")];

        var found = GlossaryMatcher.For(entries).FindEntries("Please ask the data\r\nprotection officer first.");

        Assert.That(found, Has.Count.EqualTo(1));
    }

    [Test]
    public void AnAliasMatchesForItsEntry()
    {
        IReadOnlyList<GlossaryEntry> entries = [new("Muggel", "A person without magical abilities.", ["Muggle"])];

        var found = GlossaryMatcher.For(entries).FindEntries("Harry's uncle is a Muggle.");

        Assert.That(found, Has.Count.EqualTo(1));
    }

    [Test]
    public void AnEntryAppearsOnceEvenWhenSeveralOfItsTermsMatch()
    {
        IReadOnlyList<GlossaryEntry> entries = [new("Muggel", "A person without magical abilities.", ["Muggels"])];

        var found = GlossaryMatcher.For(entries).FindEntries("Ein Muggel und noch mehr Muggels.");

        Assert.That(found, Has.Count.EqualTo(1));
    }

    [Test]
    public void ATermTooShortToMeanAnythingIsLeftOut()
    {
        IReadOnlyList<GlossaryEntry> entries = [new("a", "ein"), new(" ", "nothing")];

        var found = GlossaryMatcher.For(entries).FindEntries("a text full of the letter a");

        Assert.That(found, Is.Empty);
    }

    [Test]
    public void TheSameListGetsTheSameMatcher()
    {
        IReadOnlyList<GlossaryEntry> entries = [new("Glossar", "glossary")];

        Assert.That(GlossaryMatcher.For(entries), Is.SameAs(GlossaryMatcher.For(entries)), "The terms must be normalized only once per list.");
    }

    [Test]
    public void ALargeGlossaryAndALongTextFindAllTheirMatches()
    {
        const int TERM_COUNT = 5_000;
        var entries = Enumerable.Range(0, TERM_COUNT).Select(index => new GlossaryEntry($"Fachbegriff{index:D5}", $"term{index}")).ToList();

        var text = new StringBuilder();
        while (text.Length < 20_000)
            text.Append("Ein gewöhnlicher Satz ohne besondere Begriffe. ");

        text.Append("Am Ende stehen Fachbegriff00042 und Fachbegriff04999.");

        var matcher = GlossaryMatcher.For(entries);
        var stopwatch = Stopwatch.StartNew();
        var found = matcher.FindEntries(text.ToString());
        stopwatch.Stop();

        //
        // The time is reported, not asserted: a busy build machine must not fail the test. It tells
        // whether the plain search is still fast enough for glossaries of this size:
        //
        TestContext.Out.WriteLine($"Searching {TERM_COUNT} terms in {text.Length} characters took {stopwatch.ElapsedMilliseconds} ms.");
        Assert.That(found.Select(entry => entry.Value), Is.EqualTo(new[] { "term42", "term4999" }));
    }
}