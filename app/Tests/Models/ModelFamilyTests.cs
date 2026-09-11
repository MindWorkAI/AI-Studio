using AIStudio.Models;
using AIStudio.Models.Matching;
using AIStudio.Provider;

namespace AIStudio.Tests.Models;

/// <summary>
/// Checks how a family states its rules, and what a variant inherits from the family it belongs to.
/// </summary>
/// <remarks>
/// Inheritance here happens while the rules are being built, not while a name is being answered. A
/// variant takes what its family stated and goes on from there, and what comes out is one complete
/// rule -- so at runtime there is still exactly one selector winning, and the specificity remains
/// the only thing deciding which.
/// </remarks>
[TestFixture]
public sealed class ModelFamilyTests
{
    [Test]
    public void AFamilyNamesItselfAsTheOriginOfItsRules()
    {
        var family = new SampleFamily();

        Assert.Multiple(() =>
        {
            Assert.That(family.Name, Is.EqualTo(nameof(SampleFamily)));
            Assert.That(family.Rules.Select(rule => rule.Origin), Is.All.EqualTo(nameof(SampleFamily)));
        });
    }

    [Test]
    public void AFamilyStatesItsRulesOnlyOnce()
    {
        var family = new SampleFamily();
        var whenFirstAsked = family.Rules;
        var whenAskedAgain = family.Rules;

        Assert.That(whenAskedAgain, Is.SameAs(whenFirstAsked));
    }

    [Test]
    public void ARuleIsAboutWholeNamePartsUnlessItSaysOtherwise()
    {
        var family = new PlainFamily();

        Assert.That(family.Rules.Single().Pattern.Kind, Is.EqualTo(MatchKind.SEGMENT));
    }

    [Test]
    public void AVariantKeepsEverythingItsFamilyStatedAndOnlyChangesWhatItSays()
    {
        var index = ModelFamilyIndex.Build(new SampleFamily().Rules);
        var codex = index.Resolve(new ModelId("gpt-5.1-codex-max"), LLMProviders.OPEN_AI, ModelVendor.OPEN_AI);

        Assert.Multiple(() =>
        {
            Assert.That(codex.Has(Capability.WEB_SEARCH), Is.False, "This is the one thing the variant takes away.");
            Assert.That(codex.Has(Capability.TEXT_INPUT | Capability.MULTIPLE_IMAGE_INPUT | Capability.FUNCTION_CALLING), Is.True);
            Assert.That(codex.Reasoning, Is.EqualTo(ReasoningSupport.OPTIONAL));
            Assert.That(codex.Context.DefaultTokens, Is.EqualTo(400_000));
            Assert.That(codex.Tokenizer.Id, Is.EqualTo("o200k_base"));
        });
    }

    [Test]
    public void WhatAVariantTakesAwayIsNotTakenAwayFromTheFamily()
    {
        var index = ModelFamilyIndex.Build(new SampleFamily().Rules);
        var plain = index.Resolve(new ModelId("gpt-5.1-mini"), LLMProviders.OPEN_AI, ModelVendor.OPEN_AI);

        Assert.That(plain.Has(Capability.WEB_SEARCH), Is.True);
    }

    [Test]
    public void AVariantCanHandBackWhatItsFamilyTookAway()
    {
        var index = ModelFamilyIndex.Build(new FamilyWhichTakesSomethingBack().Rules);
        var withTools = index.Resolve(new ModelId("thing-with-tools"), LLMProviders.SELF_HOSTED, ModelVendor.UNKNOWN);

        Assert.That(withTools.Has(Capability.FUNCTION_CALLING), Is.True);
    }

    [Test]
    public void AVariantMayNameTheRuleItInheritsFromInsteadOfTakingTheOneBefore()
    {
        var index = ModelFamilyIndex.Build(new FamilyWithTwoGenerations().Rules);

        Assert.Multiple(() =>
        {
            Assert.That(index.Resolve(new ModelId("thing3-mini"), LLMProviders.SELF_HOSTED, ModelVendor.UNKNOWN).Reasoning, Is.EqualTo(ReasoningSupport.ALWAYS));
            Assert.That(index.Resolve(new ModelId("thing4"), LLMProviders.SELF_HOSTED, ModelVendor.UNKNOWN).Reasoning, Is.EqualTo(ReasoningSupport.NONE));
        });
    }

    [Test]
    public void AFirstRuleHasNothingToInheritFromAndSaysSo()
    {
        var family = new FamilyInheritingFromNothing();
        var refused = Assert.Throws<InvalidOperationException>(() => _ = family.Rules);

        Assert.That(refused?.Message, Does.Contain("first rule"));
    }

    [Test]
    public void InheritingFromARuleWhichWasNeverStatedSaysSo()
    {
        var family = new FamilyInheritingFromSomethingMissing();
        var refused = Assert.Throws<InvalidOperationException>(() => _ = family.Rules);

        Assert.That(refused?.Message, Does.Contain("does not state"));
    }

    [Test]
    public void InheritingFromARuleTextWhichNamesTwoRulesSaysSo()
    {
        //
        // Stating one text twice is ordinary: a variant of a generation is written as the same
        // pattern with a condition on top. What cannot be done afterwards is naming that text to
        // inherit from, because it no longer names one rule -- and taking whichever came last
        // would be a coin toss nobody sees.
        //
        var family = new FamilyStatingOneTextTwice();
        var refused = Assert.Throws<InvalidOperationException>(() => _ = family.Rules);

        Assert.That(refused?.Message, Does.Contain("more than once"));
    }

    [Test]
    public void AFamilyWhichAdjustsRatherThanChoosesStatesAModifier()
    {
        var family = new FamilyWithAModifier();

        Assert.That(family.Rules.Single().Kind, Is.EqualTo(ModelRuleKind.MODIFIER));
    }

    [Test]
    public void AFamilyLeavesTheProfileAloneUnlessItSaysItRefinesIt()
    {
        var family = new PlainFamily();
        var profile = new ModelProfile { Capabilities = Capability.TEXT_INPUT };

        Assert.That(family.Refine(new ModelId("thing"), profile), Is.EqualTo(profile));
    }

    [Test]
    public void ASourceWithoutAPageOrADayIsNotAStatement()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new SampleFamily().Source.IsStated, Is.True);
            Assert.That(new ModelSource(string.Empty, new DateOnly(2026, 9, 11), "a note").IsStated, Is.False);
            Assert.That(new ModelSource("https://example.invalid", default, "a note").IsStated, Is.False);
        });
    }

    /// <summary>
    /// The family from the plan, written the way a real one will be.
    /// </summary>
    private sealed class SampleFamily : ModelFamily
    {
        public override ModelVendor Vendor => ModelVendor.OPEN_AI;

        public override ModelSource Source => new("https://example.invalid/gpt-5.1", new DateOnly(2026, 9, 11), "Made up for this test, so that no real page is claimed to have been read.");

        protected override void Declare(ModelFamilyBuilder builder)
        {
            builder.Rule("gpt-5.1").AsPrefix()
                .Capabilities(Capability.TEXT_INPUT | Capability.MULTIPLE_IMAGE_INPUT | Capability.TEXT_OUTPUT | Capability.FUNCTION_CALLING | Capability.WEB_SEARCH)
                .Apis(Capability.RESPONSES_API | Capability.CHAT_COMPLETION_API)
                .Reasoning(ReasoningSupport.OPTIONAL)
                .ContextWindow(400_000)
                .Tokenizer(TokenizerKind.TIKTOKEN, "o200k_base");

            builder.Rule("gpt-5.1-codex").AsPrefix().Inherits().Removes(Capability.WEB_SEARCH);
        }
    }

    private sealed class PlainFamily : ModelFamily
    {
        public override ModelVendor Vendor => ModelVendor.UNKNOWN;

        public override ModelSource Source => new("https://example.invalid/plain", new DateOnly(2026, 9, 11), "A family stating one rule and nothing else.");

        protected override void Declare(ModelFamilyBuilder builder) => builder.Rule("thing").Capabilities(Capability.TEXT_INPUT);
    }

    private sealed class FamilyWhichTakesSomethingBack : ModelFamily
    {
        public override ModelVendor Vendor => ModelVendor.UNKNOWN;

        public override ModelSource Source => new("https://example.invalid/back", new DateOnly(2026, 9, 11), "A family whose variant regains what the family lacks.");

        protected override void Declare(ModelFamilyBuilder builder)
        {
            builder.Rule("thing").Capabilities(Capability.TEXT_INPUT).Removes(Capability.FUNCTION_CALLING);
            builder.Rule("thing-with-tools").Inherits().Capabilities(Capability.FUNCTION_CALLING);
        }
    }

    private sealed class FamilyWithTwoGenerations : ModelFamily
    {
        public override ModelVendor Vendor => ModelVendor.UNKNOWN;

        public override ModelSource Source => new("https://example.invalid/generations", new DateOnly(2026, 9, 11), "A family with two generations which reason differently.");

        protected override void Declare(ModelFamilyBuilder builder)
        {
            builder.Rule("thing3").AsPrefix().Capabilities(Capability.TEXT_INPUT).Reasoning(ReasoningSupport.ALWAYS);
            builder.Rule("thing4").AsPrefix().Capabilities(Capability.TEXT_INPUT).Reasoning(ReasoningSupport.NONE);

            // Naming the generation rather than taking whatever stands above, which here is the
            // other one:
            builder.Rule("thing3-mini").AsPrefix().InheritsFrom("thing3");
        }
    }

    private sealed class FamilyInheritingFromNothing : ModelFamily
    {
        public override ModelVendor Vendor => ModelVendor.UNKNOWN;

        public override ModelSource Source => new("https://example.invalid/nothing", new DateOnly(2026, 9, 11), "A family whose first rule inherits.");

        protected override void Declare(ModelFamilyBuilder builder) => builder.Rule("thing").Inherits();
    }

    private sealed class FamilyInheritingFromSomethingMissing : ModelFamily
    {
        public override ModelVendor Vendor => ModelVendor.UNKNOWN;

        public override ModelSource Source => new("https://example.invalid/missing", new DateOnly(2026, 9, 11), "A family inheriting from a rule it never states.");

        protected override void Declare(ModelFamilyBuilder builder)
        {
            builder.Rule("thing").Capabilities(Capability.TEXT_INPUT);
            builder.Rule("thing-mini").InheritsFrom("something-else");
        }
    }

    private sealed class FamilyStatingOneTextTwice : ModelFamily
    {
        public override ModelVendor Vendor => ModelVendor.UNKNOWN;

        public override ModelSource Source => new("https://example.invalid/twice", new DateOnly(2026, 9, 11), "A family stating one pattern text twice and then naming it to inherit from.");

        protected override void Declare(ModelFamilyBuilder builder)
        {
            builder.Rule("thing").Capabilities(Capability.TEXT_INPUT);
            builder.Rule("thing").AlsoContains("special").Capabilities(Capability.FUNCTION_CALLING);
            builder.Rule("thing-mini").InheritsFrom("thing");
        }
    }

    private sealed class FamilyWithAModifier : ModelFamily
    {
        public override ModelVendor Vendor => ModelVendor.UNKNOWN;

        public override ModelSource Source => new("https://example.invalid/modifier", new DateOnly(2026, 9, 11), "A family stating a modifier.");

        protected override void Declare(ModelFamilyBuilder builder) => builder.Modifier("base").Removes(Capability.FUNCTION_CALLING);
    }
}