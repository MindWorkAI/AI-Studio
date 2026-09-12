using AIStudio.Models;
using AIStudio.Models.Registry;
using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.Tests.Models;

/// <summary>
/// Checks the context windows the rules state, where a number was read from a vendor's page.
/// </summary>
/// <remarks>
/// The snapshot already records every one of these numbers, so this fixture is not here to catch a
/// changed answer. It is here for the handful of cases where the number is easy to get wrong by
/// writing a rule the obvious way: a generation which inherits the window of the one before it
/// although the vendor raised it, a variant which must not inherit a window at all, and the
/// question of which of two numbers a vendor states is the one a conversation is measured against.
///
/// Every number below is one somebody can check against the source its family names. A number
/// nobody could check does not belong in the rules in the first place.
/// </remarks>
[TestFixture]
public sealed class ContextWindowRuleTests
{
    [TestCase(LLMProviders.OPEN_AI, "gpt-5", 400_000, Description = "OpenAI states the whole window, input and output together.")]
    [TestCase(LLMProviders.OPEN_AI, "gpt-5.1", 400_000)]
    [TestCase(LLMProviders.OPEN_AI, "gpt-5.2", 400_000)]
    [TestCase(LLMProviders.OPEN_AI, "gpt-5.4", 1_050_000, Description = "Where the window grows in this line.")]
    [TestCase(LLMProviders.OPEN_AI, "gpt-5.5", 1_050_000)]
    [TestCase(LLMProviders.OPEN_AI, "gpt-5.6", 1_050_000)]
    [TestCase(LLMProviders.OPEN_AI, "gpt-6-astra", 1_050_000)]
    [TestCase(LLMProviders.OPEN_AI, "o1", 200_000)]
    [TestCase(LLMProviders.OPEN_AI, "o3", 200_000)]
    [TestCase(LLMProviders.OPEN_AI, "o4-mini", 200_000, Description = "The o3 generation under another number, window included.")]
    [TestCase(LLMProviders.OPEN_AI, "gpt-4o", 128_000)]
    [TestCase(LLMProviders.OPEN_AI, "gpt-4", 8_192)]
    [TestCase(LLMProviders.OPEN_AI, "gpt-4-turbo", 128_000)]
    [TestCase(LLMProviders.ANTHROPIC, "claude-3-5-sonnet-latest", 200_000)]
    [TestCase(LLMProviders.ANTHROPIC, "claude-sonnet-4-0", 200_000)]
    [TestCase(LLMProviders.ANTHROPIC, "claude-haiku-4-5-20251001", 200_000)]
    [TestCase(LLMProviders.ANTHROPIC, "claude-opus-5", 1_000_000)]
    [TestCase(LLMProviders.ANTHROPIC, "claude-sonnet-5", 1_000_000)]
    [TestCase(LLMProviders.ANTHROPIC, "claude-fable-5-1", 1_000_000)]
    [TestCase(LLMProviders.GOOGLE, "gemini-2.5-pro", 1_048_576, Description = "Google's input limit, which is what a conversation is measured against.")]
    [TestCase(LLMProviders.GOOGLE, "gemini-2.5-flash-lite", 1_048_576)]
    [TestCase(LLMProviders.GOOGLE, "gemini-3-pro", 1_000_000)]
    [TestCase(LLMProviders.GOOGLE, "gemini-flash-latest", 1_000_000)]
    [TestCase(LLMProviders.X, "grok-4.20-0309-reasoning", 1_000_000)]
    [TestCase(LLMProviders.X, "grok-build-0.1", 256_000)]
    [TestCase(LLMProviders.MISTRAL, "mistral-large-2512", 256_000)]
    [TestCase(LLMProviders.MISTRAL, "pixtral-large-2411", 128_000)]
    public void TheWindowOfAModelIsTheOneItsVendorStates(LLMProviders provider, string modelId, int tokens)
    {
        var window = provider.GetModelProfile(new Model(modelId, null)).Context;

        Assert.Multiple(() =>
        {
            Assert.That(window.IsKnown, Is.True);
            Assert.That(window.DefaultTokens, Is.EqualTo(tokens));
        });
    }

    [Test]
    public void AGenerationNobodyDocumentsInheritsNoWindowFromTheOneBeforeIt()
    {
        //
        // OpenAI has no model page for a 5.3, so the rule for it exists only to keep such a model
        // answering like the rest of its line if one ever appears. Taking 5.1's window along would
        // turn "nobody has looked this up" into a number on somebody's screen.
        //
        var profile = ModelRegistry.Shared.Profile(LLMProviders.OPEN_AI, "gpt-5.3");

        Assert.Multiple(() =>
        {
            Assert.That(profile.Context.IsKnown, Is.False);
            Assert.That(profile.Has(Capability.FUNCTION_CALLING), Is.True, "Everything else it does inherit.");
        });
    }

    [Test]
    public void TheSameModelThroughAGatewayKeepsItsWindow()
    {
        //
        // A gateway cuts what the transport cannot carry, which is about APIs. How much the model
        // reads is a property of the model and survives the trip.
        //
        var directly = ModelRegistry.Shared.Profile(LLMProviders.OPEN_AI, "gpt-5.1");
        var throughAGateway = ModelRegistry.Shared.Profile(LLMProviders.OPEN_ROUTER, "openai/gpt-5.1");

        Assert.That(throughAGateway.Context, Is.EqualTo(directly.Context));
    }

    [Test]
    public void AModelNobodyStatedAWindowForSaysSoRatherThanGuessing()
    {
        //
        // The honest answer, and the common one: most models of the open-weights world are served
        // at whatever their operator configured, so the rules state nothing and the app shows a
        // person what their conversation uses without inventing a limit for it.
        //
        var profile = ModelRegistry.Shared.Profile(LLMProviders.SELF_HOSTED, "some-model-nobody-wrote-a-rule-for");

        Assert.Multiple(() =>
        {
            Assert.That(profile.Context.IsKnown, Is.False);
            Assert.That(profile.Context.DefaultTokens, Is.Zero, "And the number next to it is meaningless, which is why nothing may read it without asking first.");
        });
    }
}