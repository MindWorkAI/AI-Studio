using AIStudio.Models;
using AIStudio.Provider;
using AIStudio.Settings;
// ReSharper disable InconsistentNaming

namespace AIStudio.Tests.Models.OpenAI;

/// <summary>
/// Checks what AI Studio assumes about an OpenAI model released after its rules were written.
/// </summary>
/// <remarks>
/// OpenAI releases faster than anybody writes rules, and a new generation is always reached by
/// somebody first. What that person must get is what every current OpenAI model does: it calls
/// tools, it looks at images, it reasons, and it answers through the Responses API. The global
/// assumption says none of that except the tools, and it sends the model to the chat completion
/// API, where a parameter meant for the Responses API is turned down.
///
/// The names below are invented on purpose. A real name belongs in the corpus; these stand for the
/// one nobody has seen yet, which is exactly the name no corpus can hold.
/// </remarks>
[TestFixture]
public sealed class NewOpenAIModelTests
{
    [TestCase("gpt-99", TestName = "A generation nobody wrote a rule for")]
    [TestCase("gpt-99-mini", TestName = "A variant of a generation nobody wrote a rule for")]
    public void ANewOpenAIModelIsAnsweredLikeTheCurrentOnes(string modelId)
    {
        var profile = LLMProviders.OPEN_AI.GetModelProfile(new Model(modelId, null));

        Assert.Multiple(() =>
        {
            Assert.That(profile.Has(Capability.RESPONSES_API), Is.True);
            Assert.That(profile.Has(Capability.FUNCTION_CALLING), Is.True);
            Assert.That(profile.Has(Capability.MULTIPLE_IMAGE_INPUT), Is.True);
            Assert.That(profile.Reasoning, Is.Not.EqualTo(ReasoningSupport.NONE));
        });
    }

    [Test]
    public void TheOpenWeightsKeepTheirOwnRuleEvenAtOpenAI()
    {
        //
        // How a pattern matches is weighed before how long it is. Written as a prefix, the
        // baseline would outrank the name part "gpt-oss" and hand the open weights reasoning and
        // the Responses API. OpenAI does not serve them today; the test is about which rule wins.
        //
        var profile = LLMProviders.OPEN_AI.GetModelProfile(new Model("gpt-oss-120b", null));

        Assert.Multiple(() =>
        {
            Assert.That(profile.Has(Capability.RESPONSES_API), Is.False);
            Assert.That(profile.Reasoning, Is.EqualTo(ReasoningSupport.NONE));
            Assert.That(profile.Context.DefaultTokens, Is.EqualTo(131_072));
        });
    }

    [Test]
    public void AModelNamedGptElsewhereIsNotTakenForAnOpenAIModel()
    {
        var profile = LLMProviders.SELF_HOSTED.GetModelProfile(new Model("gpt-j-6b", null));

        Assert.Multiple(() =>
        {
            Assert.That(profile.Has(Capability.RESPONSES_API), Is.False);
            Assert.That(profile.Has(Capability.MULTIPLE_IMAGE_INPUT), Is.False);
            Assert.That(profile.Reasoning, Is.EqualTo(ReasoningSupport.NONE));
        });
    }
}