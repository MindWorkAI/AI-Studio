using AIStudio.Models;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tests.Models.Corpus;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks the door the app asks its question through.
/// </summary>
/// <remarks>
/// Behind it stand the links of the chain in the order they win: what a person said about their own
/// installation, then what the rules worked out, then what the app assumes. The rules themselves are
/// measured elsewhere, against the whole corpus. What is measured here is the last link -- the one
/// nothing held to account until now, because a model falling through looked exactly like a model
/// nobody had asked about.
/// </remarks>
[TestFixture]
public sealed class ModelProfileChainTests
{
    [Test]
    public void EveryModelLeftToTheDefaultIsAnsweredByTheAssumption()
    {
        Assert.Multiple(() =>
        {
            foreach (var left in LeftToTheDefault.ENTRIES)
            {
                var profile = left.Provider.GetModelProfile(new Model(left.ModelId, null));
                var wanted = left.Provider is LLMProviders.NONE || string.IsNullOrWhiteSpace(left.ModelId)
                    ? Capability.NONE
                    : ModelProfile.ASSUMED.Capabilities;

                Assert.That(profile.Capabilities, Is.EqualTo(wanted), $"{left.Provider} \"{left.ModelId}\": {left.Reason}");
            }
        });
    }

    [Test]
    public void NothingLeftToTheDefaultGainsACapabilityItDoesNotHaveToday()
    {
        //
        // The direction which matters. These models lose things on the way over -- the thinking of
        // a family nobody wrote down, the image input of another -- and every loss was decided and
        // written next to the entry. What may never happen is the other direction: the switch-over
        // handing a model an ability the old rules denied it, which nobody decided and nobody would
        // see until a request comes back as an error.
        //
        Assert.Multiple(() =>
        {
            foreach (var left in LeftToTheDefault.ENTRIES)
            {
                var entry = new CorpusEntry(left.Provider, left.ModelId, CorpusOrigin.NAMED_BY_NO_RULE);
                var today = CapabilitySnapshot.AskTheCurrentRules(entry);
                var gained = RebuiltRules.AsCapabilities(left.Provider.GetModelProfile(new Model(left.ModelId, null))).Except(today).ToList();

                Assert.That(gained, Is.Empty, $"{left.Provider} \"{left.ModelId}\" would gain {string.Join(", ", gained)}, which nobody decided.");
            }
        });
    }

    [Test]
    public void AModelNoRuleKnowsReadsAndWritesTextAndCallsFunctions()
    {
        var profile = LLMProviders.SELF_HOSTED.GetModelProfile(new Model("a-model-nobody-has-heard-of", null));

        Assert.Multiple(() =>
        {
            Assert.That(profile.Has(Capability.TEXT_INPUT), Is.True);
            Assert.That(profile.Has(Capability.TEXT_OUTPUT), Is.True);
            Assert.That(profile.Has(Capability.CHAT_COMPLETION_API), Is.True);
            Assert.That(profile.Has(Capability.FUNCTION_CALLING), Is.True);
            Assert.That(profile.Has(Capability.MULTIPLE_IMAGE_INPUT), Is.False, "The assumption says nothing about what a model reads besides text.");
            Assert.That(profile.Reasoning, Is.EqualTo(ReasoningSupport.NONE));
            Assert.That(profile.Context.IsKnown, Is.False, "A context window nobody stated is unknown, not a number somebody picked.");
        });
    }

    [Test]
    public void AModelWhoseKindIsKnownKeepsItWhenTheAssumptionFillsInTheRest()
    {
        //
        // The assumption fills in the capabilities and nothing else. An embedding model nobody wrote
        // a rule for is still an embedding model, and must not turn into a chat model on the way
        // through -- it would appear in the user's chat model list and answer every request with an
        // error.
        //
        var profile = LLMProviders.SELF_HOSTED.GetModelProfile(new Model("bge-m3:567m", null));

        Assert.That(profile.Kind, Is.EqualTo(ModelKind.EMBEDDING));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void AProviderWhichNamedNoModelIsAnsweredWithNothing(string modelId)
    {
        var profile = LLMProviders.OPEN_AI.GetModelProfile(new Model(modelId, null));

        Assert.That(profile.Capabilities, Is.EqualTo(Capability.NONE), "There is nothing to assume about a model nobody picked.");
    }

    [Test]
    public void WithoutAProviderThereIsNothingToAssumeEither()
    {
        var profile = LLMProviders.NONE.GetModelProfile(new Model("gpt-5.6", null));

        Assert.That(profile.Capabilities, Is.EqualTo(Capability.NONE), "There is no way to reach the model, so there is nothing to say about how it could be used.");
    }

    [Test]
    public void WhatAPersonSaidAboutTheirOwnInstallationWinsOverTheRules()
    {
        var configured = new AIStudio.Settings.Provider(0, "test", "Test", LLMProviders.SELF_HOSTED, new Model("llama3.3:70b", null))
        {
            CapabilityOverrides = new() { MultipleImageInput = true, FunctionCalling = false },
        };

        var profile = configured.GetModelProfile();

        Assert.Multiple(() =>
        {
            Assert.That(profile.Has(Capability.MULTIPLE_IMAGE_INPUT), Is.True, "The rules say this model reads text only; the person says otherwise and can see their installation.");
            Assert.That(profile.Has(Capability.FUNCTION_CALLING), Is.False, "And the other way round.");
            Assert.That(profile.Has(Capability.TEXT_INPUT), Is.True, "Everything nobody said anything about stays as the rules had it.");
        });
    }
}