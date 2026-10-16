using AIStudio.Models;
using AIStudio.Models.Matching;
using AIStudio.Provider;

namespace AIStudio.Tests.Models.Matching;

/// <summary>
/// Checks that the index answers with the rule which says the most, whatever order it heard them in.
/// </summary>
/// <remarks>
/// The cases below are the ones the old rules got wrong, or only got right because somebody kept
/// the blocks in the right order by hand. There are no model families yet: the rules here are
/// written out in the test, because what is being checked is the engine and not what it is fed.
/// </remarks>
[TestFixture]
public sealed class ModelFamilyIndexTests
{
    private const LLMProviders ANY_PROVIDER = LLMProviders.SELF_HOSTED;

    [Test]
    public void TheRuleSayingMoreAboutANameWinsWithoutAnybodyOrderingTheRules()
    {
        //
        // This is the mistake the old rules made: the Llama block stood above the DeepSeek one, so
        // it answered for the R1 distills, which are Llama checkpoints fine-tuned on R1 answers and
        // reason where a plain Llama does not. Here neither rule knows about the other.
        //
        var llama = Selector("llama", MatchKind.SEGMENT, new() { Adds = Capability.TEXT_INPUT | Capability.FUNCTION_CALLING });
        var distill = Selector("deepseek-r1", MatchKind.SEGMENT, new() { Adds = Capability.TEXT_INPUT | Capability.FUNCTION_CALLING, Reasoning = ReasoningSupport.ALWAYS });
        var name = new ModelId("deepseek-r1-distill-llama-70b");

        Assert.Multiple(() =>
        {
            Assert.That(ModelFamilyIndex.Build([llama, distill]).Explain(name, ANY_PROVIDER, ModelVendor.UNKNOWN).Selector, Is.SameAs(distill));
            Assert.That(ModelFamilyIndex.Build([distill, llama]).Explain(name, ANY_PROVIDER, ModelVendor.UNKNOWN).Selector, Is.SameAs(distill), "The order the rules arrive in must not change the answer.");
        });
    }

    [Test]
    public void AVariantIsNotSwallowedByThePrefixItBeginsWith()
    {
        //
        // "gpt-5-chat-latest" is the alias for the GPT-5 which does not reason, and the old rules
        // told it that it always does, because the "gpt-5-" prefix claimed it first.
        //
        var reasoning = Selector("gpt-5", MatchKind.PREFIX, new() { Adds = Capability.TEXT_INPUT, Reasoning = ReasoningSupport.ALWAYS });
        var chat = Selector("gpt-5-chat", MatchKind.PREFIX, new() { Adds = Capability.TEXT_INPUT, Reasoning = ReasoningSupport.NONE });
        var index = ModelFamilyIndex.Build([reasoning, chat]);

        Assert.Multiple(() =>
        {
            Assert.That(index.Resolve(new ModelId("gpt-5-chat-latest"), ANY_PROVIDER, ModelVendor.UNKNOWN).Reasoning, Is.EqualTo(ReasoningSupport.NONE));
            Assert.That(index.Resolve(new ModelId("gpt-5-pro"), ANY_PROVIDER, ModelVendor.UNKNOWN).Reasoning, Is.EqualTo(ReasoningSupport.ALWAYS));
        });
    }

    [Test]
    public void APrefixDoesNotReachAcrossAVersionDot()
    {
        //
        // gpt-5 and gpt-5.1 are two models, and a rule written for one of them must not answer for
        // the other. Without this, every new point release would silently inherit the old answer.
        //
        var index = ModelFamilyIndex.Build([Selector("gpt-5", MatchKind.PREFIX, new() { Adds = Capability.TEXT_INPUT })]);

        Assert.That(index.Explain(new ModelId("gpt-5.1"), ANY_PROVIDER, ModelVendor.UNKNOWN).IsKnown, Is.False);
    }

    [Test]
    public void TheRuleWrittenForOneProviderWinsOnThatProviderOnly()
    {
        var openWeights = Selector("qwq", MatchKind.SEGMENT, new() { Adds = Capability.TEXT_INPUT });
        var commercial = new ModelRule(
            new() { Kind = MatchKind.SEGMENT, Text = "qwq", OnlyOn = LLMProviders.ALIBABA_CLOUD },
            ModelRuleKind.SELECTOR,
            new() { Adds = Capability.TEXT_INPUT | Capability.FUNCTION_CALLING },
            "test");

        var index = ModelFamilyIndex.Build([openWeights, commercial]);
        var name = new ModelId("qwq-32b");

        Assert.Multiple(() =>
        {
            Assert.That(index.Explain(name, LLMProviders.ALIBABA_CLOUD, ModelVendor.UNKNOWN).Selector, Is.SameAs(commercial));
            Assert.That(index.Explain(name, LLMProviders.SELF_HOSTED, ModelVendor.UNKNOWN).Selector, Is.SameAs(openWeights));
        });
    }

    [Test]
    public void AModifierAdjustsWhateverTheSelectorChose()
    {
        //
        // A base checkpoint was never instruction tuned, whatever family it comes from. In the old
        // rules that had to stand above everything else, which is why nothing below it could state
        // an exception.
        //
        var family = Selector("llama", MatchKind.SEGMENT, new() { Adds = Capability.TEXT_INPUT | Capability.FUNCTION_CALLING });
        var baseCheckpoint = Modifier("base", MatchKind.SEGMENT, new() { Removes = Capability.FUNCTION_CALLING });
        var index = ModelFamilyIndex.Build([family, baseCheckpoint]);

        Assert.Multiple(() =>
        {
            Assert.That(index.Resolve(new ModelId("llama-3.3-70b"), ANY_PROVIDER, ModelVendor.UNKNOWN).Has(Capability.FUNCTION_CALLING), Is.True);
            Assert.That(index.Resolve(new ModelId("llama-3.3-70b-base"), ANY_PROVIDER, ModelVendor.UNKNOWN).Has(Capability.FUNCTION_CALLING), Is.False);
        });
    }

    [Test]
    public void TheModifierSayingMoreHasTheLastWord()
    {
        var family = Selector("llama", MatchKind.SEGMENT, new() { Adds = Capability.TEXT_INPUT });
        var broad = Modifier("instruct", MatchKind.SEGMENT, new() { Adds = Capability.FUNCTION_CALLING });
        var narrow = Modifier("instruct-nano", MatchKind.SEGMENT, new() { Removes = Capability.FUNCTION_CALLING });
        var resolution = ModelFamilyIndex.Build([narrow, broad, family]).Explain(new ModelId("llama-3.3-instruct-nano"), ANY_PROVIDER, ModelVendor.UNKNOWN);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Modifiers.Select(modifier => modifier.Pattern.Text), Is.EqualTo(new[] { "instruct", "instruct-nano" }));
            Assert.That(resolution.Profile.Has(Capability.FUNCTION_CALLING), Is.False);
        });
    }

    [Test]
    public void AModifierAppliesOnceEvenWhenTheNameRepeatsThePartItWasFoundUnder()
    {
        var family = Selector("llama", MatchKind.SEGMENT, new() { Adds = Capability.TEXT_INPUT });
        var modifier = Modifier("llama", MatchKind.SEGMENT, new() { Adds = Capability.FUNCTION_CALLING });
        var resolution = ModelFamilyIndex.Build([family, modifier]).Explain(new ModelId("meta-llama/llama-3.3-70b"), ANY_PROVIDER, ModelVendor.UNKNOWN);

        Assert.That(resolution.Modifiers, Has.Count.EqualTo(1));
    }

    [Test]
    public void ARuleWhichCannotBeFiledUnderANamePartIsStillAsked()
    {
        //
        // A substring pattern may begin in the middle of a name part, so the index cannot narrow it
        // down and has to check it against every name. Getting that wrong would make such a rule
        // silently never fire.
        //
        var version = Selector("3.8", MatchKind.SUBSTRING, new() { Adds = Capability.TEXT_INPUT });
        var index = ModelFamilyIndex.Build([version]);

        Assert.That(index.Explain(new ModelId("qwen3.8-27b"), ANY_PROVIDER, ModelVendor.UNKNOWN).Selector, Is.SameAs(version));
    }

    [Test]
    public void TwoRulesClaimingANameWithTheSameRightAreReportedAndStillAnsweredTheSameWay()
    {
        var one = Selector("llama", MatchKind.SEGMENT, new() { Adds = Capability.TEXT_INPUT }, "family-a");
        var other = Selector("qwen3", MatchKind.SEGMENT, new() { Adds = Capability.WEB_SEARCH }, "family-b");
        var name = new ModelId("llama-qwen3-merge");

        var oneWay = ModelFamilyIndex.Build([one, other]).Explain(name, ANY_PROVIDER, ModelVendor.UNKNOWN);
        var otherWay = ModelFamilyIndex.Build([other, one]).Explain(name, ANY_PROVIDER, ModelVendor.UNKNOWN);

        Assert.Multiple(() =>
        {
            Assert.That(oneWay.IsAmbiguous, Is.True);
            Assert.That(otherWay.IsAmbiguous, Is.True);
            Assert.That(oneWay.Selector, Is.SameAs(otherWay.Selector), "Which of the two answers must not depend on the order they arrived in.");
        });
    }

    [Test]
    public void TwoRulesClaimingExactlyTheSameNamesAreFoundWhenTheIndexIsBuilt()
    {
        var one = Selector("llama", MatchKind.SEGMENT, new() { Adds = Capability.TEXT_INPUT }, "family-a");
        var other = Selector("llama", MatchKind.SEGMENT, new() { Adds = Capability.WEB_SEARCH }, "family-b");

        Assert.That(ModelFamilyIndex.Build([one, other]).Ambiguities, Has.Count.EqualTo(1));
    }

    [Test]
    public void AModifierMayShareItsPatternWithASelector()
    {
        //
        // Only selectors compete for a name; a modifier saying something about the same names is
        // the normal case and must not be reported as a conflict.
        //
        var selector = Selector("llama", MatchKind.SEGMENT, new() { Adds = Capability.TEXT_INPUT });
        var modifier = Modifier("llama", MatchKind.SEGMENT, new() { Adds = Capability.FUNCTION_CALLING });

        Assert.That(ModelFamilyIndex.Build([selector, modifier]).Ambiguities, Is.Empty);
    }

    [Test]
    public void ANameNoRuleKnowsIsAnsweredWithNothingKnown()
    {
        var index = ModelFamilyIndex.Build([Selector("llama", MatchKind.SEGMENT, new() { Adds = Capability.TEXT_INPUT })]);
        var resolution = index.Explain(new ModelId("something-nobody-wrote-a-rule-for"), ANY_PROVIDER, ModelVendor.UNKNOWN);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.IsKnown, Is.False);
            Assert.That(resolution.Profile, Is.EqualTo(ModelProfile.UNKNOWN));
        });
    }

    [Test]
    public void ANameWhichIsNothingIsNotEvenAsked()
    {
        var index = ModelFamilyIndex.Build([Selector("llama", MatchKind.SEGMENT, new() { Adds = Capability.TEXT_INPUT })]);

        Assert.That(index.Explain(new ModelId("   "), ANY_PROVIDER, ModelVendor.UNKNOWN), Is.SameAs(ModelResolution.NOTHING));
    }

    [Test]
    public void AnIndexWithoutAnyRulesAnswersInsteadOfFailing()
    {
        //
        // An index over no rules has no comparer to look name parts up with. It has nothing to look
        // up either, so it has to say so rather than throw on the first question.
        //
        var index = ModelFamilyIndex.Build([]);

        Assert.That(index.Explain(new ModelId("gpt-5.1"), ANY_PROVIDER, ModelVendor.UNKNOWN).IsKnown, Is.False);
    }

    [Test]
    public void ARuleNotWrittenInTheFormANameArrivesInIsVisibleToWhoeverAsks()
    {
        //
        // A name is lowercased on its way in, so a pattern carrying a capital letter can never
        // match anything. That is a mistake, not a rule which happens to stay quiet, and it has to
        // be findable by reading the rules rather than by noticing a model behaving oddly.
        //
        var index = ModelFamilyIndex.Build([Selector("GPT-5", MatchKind.PREFIX, new() { Adds = Capability.TEXT_INPUT })]);

        Assert.Multiple(() =>
        {
            Assert.That(index.Rules.Where(rule => !rule.Pattern.IsWellFormed), Is.Not.Empty);
            Assert.That(index.Explain(new ModelId("gpt-5.1"), ANY_PROVIDER, ModelVendor.UNKNOWN).IsKnown, Is.False);
        });
    }

    private static ModelRule Selector(string text, MatchKind kind, ModelProfileChange change, string origin = "test") => new(new() { Kind = kind, Text = text }, ModelRuleKind.SELECTOR, change, origin);

    private static ModelRule Modifier(string text, MatchKind kind, ModelProfileChange change, string origin = "test") => new(new() { Kind = kind, Text = text }, ModelRuleKind.MODIFIER, change, origin);
}