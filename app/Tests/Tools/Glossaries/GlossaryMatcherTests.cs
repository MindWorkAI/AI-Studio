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
    public void TermsWhichOverlapOrEndOtherTermsAreAllFound()
    {
        IReadOnlyList<GlossaryEntry> entries = [new("Arbeitszeit", "working hours"), new("Zeit", "time"), new("it", "information technology")];
        var matcher = GlossaryMatcher.For(entries);

        Assert.Multiple(() =>
        {
            Assert.That(matcher.FindEntries("Arbeitszeit").Select(entry => entry.Term), Is.EqualTo(new[] { "Arbeitszeit", "Zeit", "it" }));
            Assert.That(matcher.FindEntries("Arbeitsamt").Select(entry => entry.Term), Is.EqualTo(new[] { "it" }), "\"Arbeit\" contains \"it\".");
            Assert.That(matcher.FindEntries("Ein Zeugnis"), Is.Empty);
        });
    }

    [Test]
    public void EntriesWithTheSameTermAreAllFound()
    {
        IReadOnlyList<GlossaryEntry> entries = [new("Arbeitszeit", "working hours"), new("Urlaub", "leave"), new("arbeitszeit", "working time")];

        var found = GlossaryMatcher.For(entries).FindEntries("Die Arbeitszeit endet um fünf.");

        Assert.That(found.Select(entry => entry.Value), Is.EqualTo(new[] { "working hours", "working time" }));
    }

    [Test]
    public void TheAutomatonFindsWhatAPlainSearchForEveryTermFinds()
    {
        //
        // A small alphabet makes terms overlap, share beginnings, and end one another all the time;
        // the cases with case, umlauts, and whitespace runs check the normalization on the fly:
        //
        const string ALPHABET = "abAäÄ  \r\n";
        var random = new Random(4711);
        string RandomText(int maxLength) => new(Enumerable.Range(0, random.Next(maxLength + 1)).Select(_ => ALPHABET[random.Next(ALPHABET.Length)]).ToArray());

        for (var round = 0; round < 50; round++)
        {
            var entries = Enumerable.Range(0, 40)
                .Select(index => new GlossaryEntry(RandomText(6), $"value{index}", random.Next(3) == 0 ? [RandomText(6)] : null))
                .ToList();

            var matcher = GlossaryMatcher.For(entries);
            for (var textIndex = 0; textIndex < 20; textIndex++)
            {
                var text = RandomText(200);
                Assert.That(matcher.FindEntries(text), Is.EqualTo(FindEntriesPlainly(entries, text)), $"Round {round}, text {textIndex}: '{text}'");
            }
        }
    }

    [Test]
    public void TheSameListGetsTheSameMatcher()
    {
        IReadOnlyList<GlossaryEntry> entries = [new("Glossar", "glossary")];

        var first = GlossaryMatcher.For(entries);
        var second = GlossaryMatcher.For(entries);

        Assert.That(second, Is.SameAs(first), "The terms must be normalized only once per list.");
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

    [Test]
    public void AHugeTextIsReadOnce()
    {
        //
        // About a million tokens. Like the test above, this one reports its time rather than asserting
        // it; the search reads every character once, whatever the number of terms:
        //
        const int TERM_COUNT = 5_000;
        const int TEXT_LENGTH = 4_000_000;
        var entries = Enumerable.Range(0, TERM_COUNT).Select(index => new GlossaryEntry($"Fachbegriff{index:D5}", $"term{index}")).ToList();

        var text = new StringBuilder(TEXT_LENGTH + 100);
        while (text.Length < TEXT_LENGTH)
            text.Append("Ein gewöhnlicher Satz über Arbeitszeiten und Fachbegriffe ohne Nummer. ");

        text.Append("Am Ende steht Fachbegriff01234.");

        var matcher = GlossaryMatcher.For(entries);
        var stopwatch = Stopwatch.StartNew();
        var found = matcher.FindEntries(text.ToString());
        stopwatch.Stop();

        TestContext.Out.WriteLine($"Searching {TERM_COUNT} terms in {text.Length} characters took {stopwatch.ElapsedMilliseconds} ms.");
        Assert.That(found.Select(entry => entry.Value), Is.EqualTo(new[] { "term1234" }));
    }

    //
    // The search the matcher used before the automaton: the whole text normalized into a copy, then
    // every term looked for in it on its own. Slow for long texts, but obviously right:
    //
    private static List<GlossaryEntry> FindEntriesPlainly(IEnumerable<GlossaryEntry> entries, string text)
    {
        var normalizedText = Normalize(text);
        return entries
            .Where(entry => new[] { entry.Term }
                .Concat(entry.Aliases ?? [])
                .Select(Normalize)
                .Where(term => term.Length >= 2)
                .Any(term => normalizedText.Contains(term, StringComparison.Ordinal)))
            .ToList();
    }

    private static string Normalize(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}