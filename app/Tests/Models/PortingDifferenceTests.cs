using AIStudio.Models.Registry;
using AIStudio.Provider;
using AIStudio.Tests.Models.Corpus;

namespace AIStudio.Tests.Models;

/// <summary>
/// Holds the rules to the answers somebody decided on, over the whole corpus.
/// </summary>
/// <remarks>
/// While the old rules still stood, this was the test the rebuild was carried by: every model was
/// asked of both and the two had to agree, except where the audit had found the old answer wrong.
/// That comparison is over -- the old rules are gone, and the snapshot took over the job of noticing
/// when an answer changes.
///
/// What remains is the part no snapshot can do, because it is about intent rather than about
/// answers. The models the audit found wrong have to end up where the audit said. Every model
/// reaching the global assumption has to be one somebody let reach it, and every model somebody
/// listed there has to still be reaching it. And no name may be claimed by two rules with the same
/// right.
/// </remarks>
[TestFixture]
public sealed class PortingDifferenceTests
{
    [Test]
    public void EveryModelTheAuditFoundWrongIsNowAnsweredTheWayItShouldBe()
    {
        var corrected = ExpectedChanges.ENTRIES.Where(change => IsAnswered(change.Provider, change.ModelId)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(corrected, Is.Not.Empty, "Nothing the audit found wrong is answered by a rule at all, which would make this test green for the wrong reason.");

            foreach (var change in corrected)
            {
                var entry = new CorpusEntry(change.Provider, change.ModelId, CorpusOrigin.NAMED_BY_NO_RULE);
                var rebuilt = CapabilitySnapshot.Describe(RebuiltRules.Ask(entry));

                Assert.That(rebuilt, Is.EqualTo(CapabilitySnapshot.Describe(change.AnswerWanted)), $"{change.Provider} \"{change.ModelId}\": {change.Reason}");
            }
        });
    }

    [Test]
    public void EveryModelOfTheCorpusIsEitherAnsweredByARuleOrLeftToTheDefaultOnPurpose()
    {
        //
        // Comparing answers alone cannot catch a model falling through. It gets an empty profile,
        // the global default answers for it, and nothing about that looks wrong from the outside --
        // a family nobody got round to and a family nobody wanted are both simply missing. This is
        // the test which makes the difference visible, by asking for the reason.
        //
        var fallenThrough = ModelCorpus.ENTRIES
            .Where(entry => !IsAnswered(entry))
            .Where(entry => !IsLeftToTheDefault(entry))
            .Select(entry => $"{entry.Provider} \"{entry.ModelId}\"");

        Assert.That(fallenThrough, Is.Empty, "No rule answers for these, and nothing says that is on purpose. Write a family for them, or put them into LeftToTheDefault with the reason.");
    }

    [Test]
    public void NothingLeftToTheDefaultIsAnsweredByARuleAfterAll()
    {
        //
        // The other direction, so the list cannot rot: once a family is written, the models it
        // answers for have no business standing among the ones nobody wrote a rule for.
        //
        var answeredAfterAll = LeftToTheDefault.ENTRIES
            .Where(left => IsAnswered(left.Provider, left.ModelId))
            .Select(left => $"{left.Provider} \"{left.ModelId}\"");

        Assert.That(answeredAfterAll, Is.Empty, "A rule answers for these now, so they can be taken off the list of models left to the default.");
    }

    [Test]
    public void NoModelOfTheCorpusIsClaimedByTwoRulesWithTheSameRight()
    {
        //
        // Two rules of the same specificity which can both match one name are a mistake, not a coin
        // toss. Reading the rules alone cannot find it -- the two patterns are written differently
        // and only meet on a real name, which is what the corpus is full of.
        //
        Assert.Multiple(() =>
        {
            foreach (var entry in ModelCorpus.ENTRIES)
            {
                var resolution = ModelRegistry.Shared.Explain(entry.Provider, entry.ModelId);

                Assert.That(resolution.IsAmbiguous, Is.False, $"{entry.Provider} \"{entry.ModelId}\" is claimed by {resolution.Selector} and, just as strongly, by {string.Join(", ", resolution.TiedSelectors)}.");
            }
        });
    }

    /// <summary>
    /// Whether any rule knows this model.
    /// </summary>
    /// <param name="entry">The corpus entry to ask about.</param>
    /// <returns>True, when a rule answers for it.</returns>
    private static bool IsAnswered(CorpusEntry entry) => IsAnswered(entry.Provider, entry.ModelId);

    /// <summary>
    /// Whether any rule knows this model.
    /// </summary>
    /// <param name="provider">Who serves the model.</param>
    /// <param name="modelId">The model ID as that provider reports it.</param>
    /// <returns>True, when a rule answers for it.</returns>
    private static bool IsAnswered(LLMProviders provider, string modelId) => ModelRegistry.Shared.Explain(provider, modelId).IsKnown;

    /// <summary>
    /// Whether this model reaches the global default because somebody decided it may.
    /// </summary>
    /// <param name="entry">The corpus entry to look up.</param>
    /// <returns>True, when it stands in the list of models left to the default.</returns>
    private static bool IsLeftToTheDefault(CorpusEntry entry) => LeftToTheDefault.ENTRIES.Any(left => left.Provider == entry.Provider && string.Equals(left.ModelId, entry.ModelId, StringComparison.Ordinal));
}