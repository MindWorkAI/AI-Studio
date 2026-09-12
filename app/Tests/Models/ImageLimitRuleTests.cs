using AIStudio.Models;
using AIStudio.Models.Registry;
using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.Tests.Models;

/// <summary>
/// Checks how many images the rules say a model takes, where a vendor stated a number.
/// </summary>
/// <remarks>
/// Two vendors state one at all. Anthropic gives a rule rather than a number -- it reads the limit
/// off the context window -- and Google gives one number for the whole family. Everybody else either
/// says nothing or limits something other than the count: OpenAI caps the image patches of a request
/// instead of the images, which is not a number of pictures and is not written down as one here.
///
/// What is worth a test is therefore not the arithmetic but the two places where writing the rules
/// the obvious way gets it wrong: a Claude whose window grew must get the larger image limit without
/// anybody saying so, and a model nobody documented must keep answering "as many as it takes"
/// instead of inheriting somebody else's ceiling.
/// </remarks>
[TestFixture]
public sealed class ImageLimitRuleTests
{
    [TestCase(LLMProviders.ANTHROPIC, "claude-3-5-sonnet-latest", 100, Description = "A 200k window, so the smaller limit.")]
    [TestCase(LLMProviders.ANTHROPIC, "claude-sonnet-4-0", 100)]
    [TestCase(LLMProviders.ANTHROPIC, "claude-haiku-4-5-20251001", 100)]
    [TestCase(LLMProviders.ANTHROPIC, "claude-opus-5", 600, Description = "A million tokens, so Anthropic's limit for every other model.")]
    [TestCase(LLMProviders.ANTHROPIC, "claude-sonnet-5", 600)]
    [TestCase(LLMProviders.ANTHROPIC, "claude-opus-4-6", 600)]
    [TestCase(LLMProviders.ANTHROPIC, "claude-sonnet-4-6", 600)]
    [TestCase(LLMProviders.ANTHROPIC, "claude-fable-5-1", 600)]
    [TestCase(LLMProviders.GOOGLE, "gemini-2.5-pro", 3_600, Description = "Google states one number for all of Gemini.")]
    [TestCase(LLMProviders.GOOGLE, "gemini-3-pro", 3_600)]
    [TestCase(LLMProviders.GOOGLE, "gemini-flash-latest", 3_600)]
    public void TheImageLimitOfAModelIsTheOneItsVendorStates(LLMProviders provider, string modelId, int perRequest)
    {
        var limits = provider.GetModelProfile(new Model(modelId, null)).Images;

        Assert.Multiple(() =>
        {
            Assert.That(limits.IsKnown, Is.True);
            Assert.That(limits.MaxPerRequest, Is.EqualTo(perRequest));
            Assert.That(limits.MaxPerMessage, Is.Null, "Neither vendor states a per-message limit, and inventing one would be a ceiling nobody wrote.");
        });
    }

    [Test]
    public void AClaudeWhoseWindowGrewGetsTheLargerImageLimitWithoutSayingSo()
    {
        //
        // This is the whole reason the limit is worked out instead of written down: the rule for
        // Opus 4.6 states its larger window and nothing else, and Anthropic's own page says the
        // image limit follows from exactly that. Two numbers written by hand would have drifted the
        // first time somebody added a model and thought of only one of them.
        //
        var smallWindow = ModelRegistry.Shared.Profile(LLMProviders.ANTHROPIC, "claude-opus-4-1");
        var largeWindow = ModelRegistry.Shared.Profile(LLMProviders.ANTHROPIC, "claude-opus-4-6");

        Assert.Multiple(() =>
        {
            Assert.That(smallWindow.Context.DefaultTokens, Is.EqualTo(200_000));
            Assert.That(smallWindow.Images.MaxPerRequest, Is.EqualTo(100));
            Assert.That(largeWindow.Context.DefaultTokens, Is.EqualTo(1_000_000));
            Assert.That(largeWindow.Images.MaxPerRequest, Is.EqualTo(600));
        });
    }

    [Test]
    public void AModelNobodyStatedALimitForTakesAsManyAsItTakes()
    {
        //
        // The common case, and the one which must not become a hidden ceiling. A self-hosted model
        // is served at whatever its operator configured, and an app which refused the seventh
        // picture because six is a nice number would be taking something away that works today.
        //
        var profile = ModelRegistry.Shared.Profile(LLMProviders.SELF_HOSTED, "some-model-nobody-wrote-a-rule-for");

        Assert.Multiple(() =>
        {
            Assert.That(profile.Images.IsKnown, Is.False);
            Assert.That(profile.Images.MaxInOneMessage, Is.Null);
        });
    }

    [Test]
    public void OpenAIStatesNoNumberOfImagesAndSoNeitherDoWe()
    {
        //
        // Their guide caps a request at 30,000 image patches, which is a budget rather than a count:
        // how many pictures fit into it depends on how large each of them is. Writing any number of
        // images here would be our arithmetic presented as their statement.
        //
        var profile = ModelRegistry.Shared.Profile(LLMProviders.OPEN_AI, "gpt-5.1");

        Assert.Multiple(() =>
        {
            Assert.That(profile.Has(Capability.MULTIPLE_IMAGE_INPUT), Is.True, "It does read several images.");
            Assert.That(profile.Images.IsKnown, Is.False, "How many, nobody said.");
        });
    }

    [Test]
    public void TheSameModelThroughAGatewayKeepsItsImageLimit()
    {
        //
        // A gateway cuts what its transport cannot carry, which is about APIs. How many images the
        // model reads is a property of the model and survives the trip.
        //
        var directly = ModelRegistry.Shared.Profile(LLMProviders.ANTHROPIC, "claude-opus-5");
        var throughAGateway = ModelRegistry.Shared.Profile(LLMProviders.OPEN_ROUTER, "anthropic/claude-opus-5");

        Assert.That(throughAGateway.Images, Is.EqualTo(directly.Images));
    }

    [TestCase(null, null, null, Description = "Nobody stated either, so there is nothing to go by.")]
    [TestCase(8, null, 8)]
    [TestCase(null, 100, 100)]
    [TestCase(8, 100, 8, Description = "A message is part of a request, so the smaller of the two decides.")]
    [TestCase(100, 8, 8)]
    [TestCase(0, null, 0, Description = "Zero is a real answer: an operator can configure an engine to take no images at all.")]
    public void WhatMayTravelInOneMessageIsTheSmallerOfWhatIsKnown(int? perMessage, int? perRequest, int? expected)
    {
        var limits = new ImageLimits(perMessage, perRequest);

        Assert.That(limits.MaxInOneMessage, Is.EqualTo(expected));
    }
}