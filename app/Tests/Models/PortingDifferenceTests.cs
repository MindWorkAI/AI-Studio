using AIStudio.Models;
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
/// The two lists below are what grow. A provider not on either is simply not compared yet: its
/// models reach rules which have not been written, and holding them to anything would only say
/// that.
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
        LLMProviders.ALIBABA_CLOUD,
        LLMProviders.DEEP_SEEK,
        LLMProviders.PERPLEXITY,
        LLMProviders.X,
    ];

    /// <summary>
    /// The vendors whose models the rebuilt rules already answer for, whoever serves them.
    /// </summary>
    /// <remarks>
    /// Open weights are the reason this list exists next to the one above. They arrive through the
    /// gateways and the local engines, and none of those can be called ported until every vendor
    /// they carry is. The vendor is the unit the porting actually proceeds in: as soon as the Llama
    /// rules exist, every Llama of the corpus is compared, whichever gateway it came from.
    ///
    /// It is also what finally holds the cloud vendors to their models on somebody else's gateway,
    /// which the provider list alone never reached: a Claude at GWDG and a DeepSeek distill at
    /// OpenRouter are compared here, not at Anthropic and not at DeepSeek.
    ///
    /// Only vendors, never UNKNOWN: that is what a model nobody wrote a rule for answers with, and
    /// putting it here would compare everything against everything.
    ///
    /// Mistral is the one ported vendor still missing. Its cloud writes a release date into every
    /// name and its rules are built on that, while the open weights are called
    /// "mistral-small-3.1-24b-instruct" and carry none -- so those names are still to be ported.
    /// </remarks>
    private static readonly IReadOnlyList<ModelVendor> VENDORS_ALREADY_PORTED =
    [
        ModelVendor.OPEN_AI,
        ModelVendor.ANTHROPIC,
        ModelVendor.GOOGLE,
        ModelVendor.ALIBABA,
        ModelVendor.DEEP_SEEK,
        ModelVendor.PERPLEXITY,
        ModelVendor.XAI,
        ModelVendor.META,
    ];

    /// <summary>
    /// Who built the models of each family, by the name its rules name as their origin.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, ModelVendor> VENDOR_OF_FAMILY = ModelRegistry.Shared.Families.ToDictionary(family => family.Name, family => family.Vendor, StringComparer.Ordinal);

    [Test]
    public void EveryPortedModelGetsExactlyTheAnswerItGetsToday()
    {
        var compared = ComparableEntries().Where(entry => !IsKnownToBeWrong(entry)).ToList();

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
        var ported = ExpectedChanges.ENTRIES.Where(change => IsCompared(change.Provider, change.ModelId)).ToList();

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
        // Only the ported providers, and on purpose: a model is on the vendor list exactly because
        // a rule answered for it, so asking those the same question would answer itself.
        //
        var named = EntriesOfPortedProviders().Where(entry => !string.IsNullOrWhiteSpace(entry.ModelId)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(named, Is.Not.Empty, "Nothing was asked about at all, which would make this test green for the wrong reason.");

            foreach (var entry in named)
                Assert.That(ModelRegistry.Shared.Explain(entry.Provider, entry.ModelId).IsKnown, Is.True, $"No rule answers for {entry.Provider} \"{entry.ModelId}\".");
        });
    }

    [Test]
    public void NoModelOfTheCorpusIsClaimedByTwoRulesWithTheSameRight()
    {
        //
        // The whole corpus, ported or not: a tie needs two rules that both exist, so every name is
        // worth asking about as soon as anything answers for it. This is where a family which
        // repeats what another one already said shows up -- reading the rules alone cannot find
        // that, because the two patterns are written differently and only meet on a real name.
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
    /// Every corpus entry of a provider which has been ported, known-wrong ones included.
    /// </summary>
    /// <returns>The entries.</returns>
    private static IEnumerable<CorpusEntry> EntriesOfPortedProviders() => ModelCorpus.ENTRIES.Where(entry => PROVIDERS_ALREADY_PORTED.Contains(entry.Provider));

    /// <summary>
    /// Every corpus entry the rebuilt rules are already meant to answer for.
    /// </summary>
    /// <returns>The entries.</returns>
    private static IEnumerable<CorpusEntry> ComparableEntries() => ModelCorpus.ENTRIES.Where(entry => IsCompared(entry.Provider, entry.ModelId));

    /// <summary>
    /// Whether the rebuilt rules are held to what the old ones answer for this model.
    /// </summary>
    /// <param name="provider">Who serves the model.</param>
    /// <param name="modelId">The model ID as that provider reports it.</param>
    /// <returns>True, when the two answers have to agree.</returns>
    private static bool IsCompared(LLMProviders provider, string modelId) => PROVIDERS_ALREADY_PORTED.Contains(provider) || VENDORS_ALREADY_PORTED.Contains(VendorAnswering(provider, modelId));

    /// <summary>
    /// Whose rules answered for a model, as far as any did.
    /// </summary>
    /// <param name="provider">Who serves the model.</param>
    /// <param name="modelId">The model ID as that provider reports it.</param>
    /// <returns>The vendor of the family whose rule chose, or UNKNOWN when none did.</returns>
    private static ModelVendor VendorAnswering(LLMProviders provider, string modelId)
    {
        var selector = ModelRegistry.Shared.Explain(provider, modelId).Selector;
        return selector is null ? ModelVendor.UNKNOWN : VENDOR_OF_FAMILY.GetValueOrDefault(selector.Origin, ModelVendor.UNKNOWN);
    }

    /// <summary>
    /// Whether the audit found the current answer for this entry wrong.
    /// </summary>
    /// <param name="entry">The entry to look up.</param>
    /// <returns>True, when the rebuild is meant to answer differently.</returns>
    private static bool IsKnownToBeWrong(CorpusEntry entry) => ExpectedChanges.ENTRIES.Any(change => change.Provider == entry.Provider && change.ModelId == entry.ModelId);
}