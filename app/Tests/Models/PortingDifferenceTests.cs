using AIStudio.Models.Registry;
using AIStudio.Provider;
using AIStudio.Tests.Models.Corpus;

namespace AIStudio.Tests.Models;

/// <summary>
/// Holds the rebuilt rules against the old ones, provider by provider, as the porting proceeds.
/// </summary>
/// <remarks>
/// This is the test the whole rebuild is being carried by. For every provider already ported, the
/// new rules have to answer exactly what the old ones answer -- except where the audit found the
/// old answer wrong, and there they have to answer what was written down instead. Anything else is
/// either a porting mistake or a decision somebody has to make on purpose and record.
///
/// The list below is what grows. A provider not on it is simply not compared yet: its models reach
/// rules which have not been written, and holding them to anything would only say that.
/// </remarks>
[TestFixture]
public sealed class PortingDifferenceTests
{
    /// <summary>
    /// The providers whose models the rebuilt rules already answer for.
    /// </summary>
    /// <remarks>
    /// A vendor's own cloud comes first, because there a name arrives the way its vendor writes it.
    /// The gateways and the self-hosted engines come last: they serve everybody's models, so they
    /// are only fully answerable once everybody has been ported.
    /// </remarks>
    private static readonly IReadOnlyList<LLMProviders> PROVIDERS_ALREADY_PORTED =
    [
        LLMProviders.OPEN_AI,
        LLMProviders.ANTHROPIC,
        LLMProviders.GOOGLE,
        LLMProviders.MISTRAL,
    ];

    [Test]
    public void EveryPortedModelGetsExactlyTheAnswerItGetsToday()
    {
        var compared = PortedEntries().Where(entry => !IsKnownToBeWrong(entry)).ToList();

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
    public void EveryPortedModelTheAuditFoundWrongIsNowAnsweredTheWayItShouldBe()
    {
        var ported = ExpectedChanges.ENTRIES.Where(change => PROVIDERS_ALREADY_PORTED.Contains(change.Provider)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(ported, Is.Not.Empty, "No ported provider has an entry the audit found wrong, so this test proves nothing. Check the list of ported providers.");

            foreach (var change in ported)
            {
                var entry = new CorpusEntry(change.Provider, change.ModelId, CorpusOrigin.NAMED_BY_NO_RULE);
                var rebuilt = CapabilitySnapshot.Describe(RebuiltRules.Ask(entry));

                Assert.That(rebuilt, Is.EqualTo(CapabilitySnapshot.Describe(change.AnswerWanted)), $"{change.Provider} \"{change.ModelId}\": {change.Reason}");
            }
        });
    }

    [Test]
    public void EveryPortedModelIsAnsweredByARuleRatherThanFallingThrough()
    {
        //
        // Comparing answers alone cannot catch this. A model nobody wrote a rule for gets an empty
        // profile, and where the old answer was empty too, the comparison is happy -- while the
        // model has in fact disappeared from the rules. This is the test which notices.
        //
        var named = PortedEntries().Where(entry => !string.IsNullOrWhiteSpace(entry.ModelId)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(named, Is.Not.Empty, "Nothing was asked about at all, which would make this test green for the wrong reason.");

            foreach (var entry in named)
            {
                var resolution = ModelRegistry.Shared.Explain(entry.Provider, entry.ModelId);

                Assert.That(resolution.IsKnown, Is.True, $"No rule answers for {entry.Provider} \"{entry.ModelId}\".");
                Assert.That(resolution.IsAmbiguous, Is.False, $"{entry.Provider} \"{entry.ModelId}\" is claimed by {resolution.Selector} and, just as strongly, by {string.Join(", ", resolution.TiedSelectors)}.");
            }
        });
    }

    /// <summary>
    /// Every corpus entry of a provider which has been ported, known-wrong ones included.
    /// </summary>
    /// <returns>The entries.</returns>
    private static IEnumerable<CorpusEntry> PortedEntries() => ModelCorpus.ENTRIES.Where(entry => PROVIDERS_ALREADY_PORTED.Contains(entry.Provider));

    /// <summary>
    /// Whether the audit found the current answer for this entry wrong.
    /// </summary>
    /// <param name="entry">The entry to look up.</param>
    /// <returns>True, when the rebuild is meant to answer differently.</returns>
    private static bool IsKnownToBeWrong(CorpusEntry entry) => ExpectedChanges.ENTRIES.Any(change => change.Provider == entry.Provider && change.ModelId == entry.ModelId);
}