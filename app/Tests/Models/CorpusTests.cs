using AIStudio.Provider;
using AIStudio.Tests.Models.Corpus;

namespace AIStudio.Tests.Models;

/// <summary>
/// Checks the corpus itself, before it is used to judge anything.
/// </summary>
/// <remarks>
/// A ruler has to be straight before it can measure. A duplicate entry would silently outvote
/// itself in the snapshot, and an entry which no longer belongs to any corpus model would make the
/// list of known-wrong answers point at nothing.
/// </remarks>
[TestFixture]
public sealed class CorpusTests
{
    [Test]
    public void NoModelAppearsTwiceForTheSameProvider()
    {
        var duplicates = ModelCorpus.ENTRIES
            .GroupBy(entry => (entry.Provider, entry.ModelId))
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key.Provider} {group.Key.ModelId}")
            .ToList();

        Assert.That(duplicates, Is.Empty);
    }

    [Test]
    public void EveryKnownWrongAnswerBelongsToAModelOfTheCorpus()
    {
        var corpus = ModelCorpus.ENTRIES.Select(entry => (entry.Provider, entry.ModelId)).ToHashSet();
        var orphans = ExpectedChanges.ENTRIES
            .Where(change => !corpus.Contains((change.Provider, change.ModelId)))
            .Select(change => $"{change.Provider} {change.ModelId}")
            .ToList();

        Assert.That(orphans, Is.Empty);
    }

    [Test]
    public void EveryKnownWrongAnswerSaysWhyAndWhereThatCanBeChecked()
    {
        Assert.Multiple(() =>
        {
            foreach (var change in ExpectedChanges.ENTRIES)
            {
                Assert.That(change.Reason, Is.Not.Empty, $"{change.Provider} {change.ModelId} does not say why the current answer is wrong.");
                Assert.That(change.Source, Is.Not.Empty, $"{change.Provider} {change.ModelId} does not say where that can be checked.");
                Assert.That(change.AnswerWanted, Is.Not.Empty, $"{change.Provider} {change.ModelId} does not say what the answer should be.");
            }
        });
    }

    [Test]
    public void EveryProviderTheAppSupportsIsRepresented()
    {
        //
        // A provider missing from the corpus is a whole branch of the dispatch nobody measures.
        // That includes the ones without rules of their own: which rules they borrow, and what
        // happens to the answer on the way back, is exactly the part a rebuild gets wrong.
        //
        var covered = ModelCorpus.ENTRIES.Select(entry => entry.Provider).ToHashSet();
        var missing = Enum.GetValues<LLMProviders>().Where(provider => !covered.Contains(provider)).ToList();

        Assert.That(missing, Is.Empty);
    }
}