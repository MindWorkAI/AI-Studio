using AIStudio.Models;
using AIStudio.Models.Live;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tests.Models.Corpus;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks the door the app asks its question through.
/// </summary>
/// <remarks>
/// Behind it stand the links of the chain in the order they win: what a person said about their own
/// installation, then what that installation reported about itself, then what the rules worked out,
/// then what the app assumes. The rules themselves are measured elsewhere, against the whole corpus.
/// What is measured here is everything around them -- the last link, which nothing held to account
/// until now because a model falling through looked exactly like a model nobody had asked about,
/// and the order of the three links above it, each of which can speak about the same number.
///
/// What a provider reported lands in the store the app shares, so these tests must not run next to
/// anything else touching it.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ModelProfileChainTests
{
    private const string MACHINE = "33333333-3333-3333-3333-333333333333";

    /// <summary>
    /// A window no rule would ever state, so that finding it proves where the answer came from.
    /// </summary>
    private const int WHAT_THE_MACHINE_REPORTS = 33_333;

    private static readonly Model MODEL = new("qwen3-32b", null);

    [SetUp]
    public void ForgetWhatTheMachineSaidBefore() => ListedModels.Shared.Report(MACHINE, []);

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

    [Test]
    public void WhatThePersonTypedBeatsWhatTheMachineReported()
    {
        //
        // Somebody who types a window has a reason for it, and the app is not in a position to know
        // it better -- they may be working around an engine reporting nonsense.
        //
        ListedModels.Shared.Report(MACHINE, [new(MODEL.Id, ContextWindow.Of(WHAT_THE_MACHINE_REPORTS))]);
        var configured = ProviderWith(new() { ContextWindowTokens = 8_192 });

        Assert.That(configured.GetModelProfile().Context.DefaultTokens, Is.EqualTo(8_192));
    }

    [Test]
    public void WhatTheMachineReportedBeatsWhatTheRulesWorkedOut()
    {
        ListedModels.Shared.Report(MACHINE, [new(MODEL.Id, ContextWindow.Of(WHAT_THE_MACHINE_REPORTS))]);
        var configured = ProviderWith(null);

        Assert.That(configured.GetModelProfile().Context.DefaultTokens, Is.EqualTo(WHAT_THE_MACHINE_REPORTS));
    }

    [Test]
    public void ASilentMachineLeavesTheRulesStanding()
    {
        var configured = ProviderWith(null);

        Assert.That(configured.GetModelProfile().Context, Is.EqualTo(LLMProviders.SELF_HOSTED.GetModelProfile(MODEL).Context));
    }

    [Test]
    public void TheAutomaticAnswerIsWhatHappensWithoutTheSwitches()
    {
        //
        // This is the number the expert dialog offers as its placeholder. Showing the rules there
        // while the chat goes by the reported window would tell a person that emptying the field
        // gets them something it does not.
        //
        ListedModels.Shared.Report(MACHINE, [new(MODEL.Id, ContextWindow.Of(WHAT_THE_MACHINE_REPORTS))]);
        var configured = ProviderWith(new() { ContextWindowTokens = 8_192 });

        Assert.Multiple(() =>
        {
            Assert.That(configured.GetAutomaticModelProfile().Context.DefaultTokens, Is.EqualTo(WHAT_THE_MACHINE_REPORTS));
            Assert.That(configured.GetModelProfile().Context.DefaultTokens, Is.EqualTo(8_192), "What the person typed is still what counts everywhere else.");
        });
    }

    [Test]
    public void WhatOneMachineReportsIsNoAnswerForAnother()
    {
        ListedModels.Shared.Report(MACHINE, [new(MODEL.Id, ContextWindow.Of(WHAT_THE_MACHINE_REPORTS))]);
        var somebodyElse = ProviderWith(null) with { Id = "44444444-4444-4444-4444-444444444444" };

        Assert.That(somebodyElse.GetModelProfile().Context, Is.EqualTo(LLMProviders.SELF_HOSTED.GetModelProfile(MODEL).Context));
    }

    /// <summary>
    /// A configured self-hosted provider, the way the settings hold one.
    /// </summary>
    /// <param name="overrides">What the person switched, or nothing when they switched nothing.</param>
    /// <returns>The configured provider.</returns>
    private static AIStudio.Settings.Provider ProviderWith(ProviderCapabilityOverrides? overrides) => new(1, MACHINE, "A machine of my own", LLMProviders.SELF_HOSTED, MODEL, IsSelfHosted: true)
    {
        CapabilityOverrides = overrides,
    };
}