using AIStudio.Models;
using AIStudio.Models.Matching;
using AIStudio.Models.Registry;
using AIStudio.Provider;
using AIStudio.Tests.Models.Corpus;

namespace AIStudio.Tests.Models.Registry;

/// <summary>
/// Checks the registry itself, and the properties every rule in the app has to have.
/// </summary>
/// <remarks>
/// The property tests below are the ones which cannot be written per family, because what they ask
/// about only exists once all the families are together: whether two of them claim the same name,
/// whether every rule can be traced back to somebody. They are cheap and they grow with the rules
/// on their own, which is the point -- nobody has to remember to extend them when adding a family.
/// </remarks>
[TestFixture]
public sealed class ModelRegistryTests
{
    [Test]
    public void NoTwoRulesOfTheAppClaimTheSameNamesWithTheSameRight()
    {
        var ambiguities = ModelRegistry.Shared.Rules.Ambiguities.Select(ambiguity => $"{ambiguity.First} / {ambiguity.Second}: {ambiguity.Reason}");

        Assert.That(ambiguities, Is.Empty);
    }

    [Test]
    public void EveryRuleIsWrittenInTheFormNamesArriveIn()
    {
        //
        // The compile time rule says the same thing about every literal in the source. This says it
        // about the rules as they were actually built, which also covers a pattern that was put
        // together rather than written down.
        //
        var malformed = ModelRegistry.Shared.Rules.Rules.Where(rule => !rule.Pattern.IsWellFormed).Select(rule => rule.Description);

        Assert.That(malformed, Is.Empty);
    }

    [Test]
    public void EveryFamilySaysWhereItsStatementsCanBeCheckedAndWhen()
    {
        //
        // The further sources are asked the same question as the first one. A family which reads
        // its windows from one page and its image limits from another has two pages to name, and a
        // second page named without a day is exactly as uncheckable as no page at all.
        //
        var unstated = ModelRegistry.Shared.Families
            .Where(family => !family.Source.IsStated || family.FurtherSources.Any(source => !source.IsStated))
            .Select(family => family.Name);

        Assert.That(unstated, Is.Empty);
    }

    [Test]
    public void NoFamilyStatesOneOfTheThreeReasoningWords()
    {
        //
        // They are override vocabulary: a person writes ALWAYS_REASONING to correct us, and a
        // profile answers the same question through its reasoning field, where the contradictory
        // combinations cannot be written down. A family reaching for the flag would be stating
        // something the profile then silently drops.
        //
        var confused = ModelRegistry.Shared.Rules.Rules
            .Where(rule => (rule.Change.Adds & ModelProfile.REASONING_VOCABULARY) is not Capability.NONE)
            .Select(rule => rule.Description);

        Assert.That(confused, Is.Empty, "State how a model reasons with Reasoning(...) instead.");
    }

    [Test]
    public void EveryRuleNamesAFamilyTheRegistryCanFindAgain()
    {
        //
        // The origin is how a rule finds its way back to the family which wrote it, and that is what
        // decides whose Refine is asked. A name which leads nowhere would simply skip the refining.
        //
        var families = ModelRegistry.Shared.Families.Select(family => family.Name).ToHashSet(StringComparer.Ordinal);
        var orphans = ModelRegistry.Shared.Rules.Rules.Where(rule => !families.Contains(rule.Origin)).Select(rule => rule.Description);

        Assert.That(orphans, Is.Empty);
    }

    [Test]
    public void WithoutAProviderThereIsNothingToSayAboutAModel()
    {
        //
        // A model is reached through a provider, and without one there is no way to reach it. The
        // rules this replaces answered the same, by having no branch for it at all.
        //
        var profile = ModelRegistry.Shared.Profile(LLMProviders.NONE, "gpt-5.6");

        Assert.That(RebuiltRules.AsCapabilities(profile), Is.Empty);
    }

    [TestCase("")]
    [TestCase("   ")]
    public void AProviderWhichNamedNoModelIsAnsweredWithNothing(string modelId)
    {
        var profile = ModelRegistry.Shared.Profile(LLMProviders.OPEN_AI, modelId);

        Assert.That(RebuiltRules.AsCapabilities(profile), Is.Empty);
    }

    [Test]
    public void TheSameModelReachedTwoWaysGetsTwoAnswers()
    {
        //
        // Also the test that the remembered answers are kept per provider: one key for both would
        // hand whichever was asked first to the other.
        //
        var atOpenAI = ModelRegistry.Shared.Profile(LLMProviders.OPEN_AI, "gpt-5.1");
        var throughAGateway = ModelRegistry.Shared.Profile(LLMProviders.OPEN_ROUTER, "openai/gpt-5.1");

        Assert.Multiple(() =>
        {
            Assert.That(atOpenAI.Has(Capability.RESPONSES_API), Is.True);
            Assert.That(throughAGateway.Has(Capability.RESPONSES_API), Is.False);
            Assert.That(throughAGateway.Has(Capability.FUNCTION_CALLING), Is.True, "Everything but the API survives the trip through a gateway.");
        });
    }

    [Test]
    public void TheFamilyWhichChoseTheModelGetsToWorkSomethingOutOfTheName()
    {
        var registry = ModelRegistry.Build([new RefiningFamily()], []);
        var profile = registry.Profile(LLMProviders.SELF_HOSTED, "refined-thing");

        Assert.That(profile.Has(Capability.WEB_SEARCH), Is.True, "The family adds this in Refine, which no rule can express.");
    }

    [Test]
    public void TwoFamiliesOfTheSameNameAreRefused()
    {
        var refused = Assert.Throws<InvalidOperationException>(() => ModelRegistry.Build([new FirstPlace.TwiceNamedFamily(), new SecondPlace.TwiceNamedFamily()], []));

        Assert.That(refused?.Message, Does.Contain(nameof(FirstPlace.TwiceNamedFamily)));
    }

    private sealed class RefiningFamily : ModelFamily
    {
        public override ModelVendor Vendor => ModelVendor.UNKNOWN;

        public override ModelSource Source => new("https://example.invalid/refining", new DateOnly(2026, 9, 11), "A family which works something out of the name after a rule chose it.");

        public override ModelProfile Refine(in ModelId id, in ModelProfile selected) => selected with { Capabilities = selected.Capabilities | Capability.WEB_SEARCH };

        protected override void Declare(ModelFamilyBuilder builder) => builder.Rule("refined").Capabilities(Capability.TEXT_INPUT).Apis(Capability.CHAT_COMPLETION_API);
    }

    private static class FirstPlace
    {
        internal sealed class TwiceNamedFamily : ModelFamily
        {
            public override ModelVendor Vendor => ModelVendor.UNKNOWN;

            public override ModelSource Source => new("https://example.invalid/first", new DateOnly(2026, 9, 11), "One of two families sharing a name.");

            protected override void Declare(ModelFamilyBuilder builder) => builder.Rule("first");
        }
    }

    private static class SecondPlace
    {
        internal sealed class TwiceNamedFamily : ModelFamily
        {
            public override ModelVendor Vendor => ModelVendor.UNKNOWN;

            public override ModelSource Source => new("https://example.invalid/second", new DateOnly(2026, 9, 11), "The other of two families sharing a name.");

            protected override void Declare(ModelFamilyBuilder builder) => builder.Rule("second");
        }
    }
}