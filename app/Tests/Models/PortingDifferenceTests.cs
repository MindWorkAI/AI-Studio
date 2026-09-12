using AIStudio.Models.Registry;
using AIStudio.Provider;
using AIStudio.Tests.Models.Corpus;

namespace AIStudio.Tests.Models;

/// <summary>
/// Holds the rebuilt rules against the old ones, over the whole corpus.
/// </summary>
/// <remarks>
/// This is the test the whole rebuild is being carried by. Every model the new rules answer for has
/// to be answered exactly the way the old ones answer it -- except where the audit found the old
/// answer wrong, and there it has to be answered the way ExpectedChanges says instead. Anything
/// else is either a porting mistake or a decision somebody has to make on purpose and record.
///
/// While the porting was under way, this was scoped by two growing lists: first the providers whose
/// models had rules, then the vendors, because open weights arrive through gateways which serve
/// everybody. Both are gone now that every family exists. What is left is simpler and says more:
/// whatever a rule answers is compared, and whatever no rule answers has to stand in
/// LeftToTheDefault with a reason.
/// </remarks>
[TestFixture]
public sealed class PortingDifferenceTests
{
    [Test]
    public void EveryModelTheRulesAnswerForGetsExactlyTheAnswerItGetsToday()
    {
        var compared = ModelCorpus.ENTRIES.Where(IsAnswered).Where(entry => !IsKnownToBeWrong(entry)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(compared, Is.Not.Empty, "Nothing was compared at all, which would make this test green for the wrong reason.");

            foreach (var entry in compared)
            {
                var today = CapabilitySnapshot.Describe(CapabilitySnapshot.AskTheCurrentRules(entry));
                var rebuilt = CapabilitySnapshot.Describe(RebuiltRules.Ask(entry));

                Assert.That(rebuilt, Is.EqualTo(today), $"{entry.Provider} \"{entry.ModelId}\" is answered differently by the rebuilt rules.");
            }
        });
    }

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

    /// <summary>
    /// Whether the audit found the current answer for this entry wrong.
    /// </summary>
    /// <param name="entry">The entry to look up.</param>
    /// <returns>True, when the rebuild is meant to answer differently.</returns>
    private static bool IsKnownToBeWrong(CorpusEntry entry) => ExpectedChanges.ENTRIES.Any(change => change.Provider == entry.Provider && change.ModelId == entry.ModelId);
}