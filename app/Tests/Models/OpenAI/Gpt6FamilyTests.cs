using AIStudio.Models;
using AIStudio.Provider;
using AIStudio.Settings;
// ReSharper disable InconsistentNaming

namespace AIStudio.Tests.Models.OpenAI;

/// <summary>
/// Checks what AI Studio knows about the GPT-6 models when it reaches them at OpenAI itself.
/// </summary>
/// <remarks>
/// OpenAI serves three GPT-6 models, and for a while the rules knew one of them. The other two fell
/// through to the global assumption, which puts a model on the chat completion API with neither
/// images nor web search nor reasoning. Nothing looked wrong from the outside until a request
/// carrying a Responses API parameter was turned down.
///
/// The questions go through the same chain the OpenAI provider asks, assumption included, because
/// that is where a model which no rule knows ends up looking like one somebody described.
/// </remarks>
[TestFixture]
public sealed class Gpt6FamilyTests
{
    [TestCase("gpt-6-astra", TestName = "Astra is reached through the Responses API")]
    [TestCase("gpt-6-sol", TestName = "Sol is reached through the Responses API")]
    [TestCase("gpt-6-luna", TestName = "Luna is reached through the Responses API")]
    public void EveryGpt6ModelIsAnsweredLikeTheModelPageDescribesIt(string modelId)
    {
        var profile = LLMProviders.OPEN_AI.GetModelProfile(new Model(modelId, null));

        Assert.Multiple(() =>
        {
            Assert.That(profile.Has(Capability.RESPONSES_API), Is.True);
            Assert.That(profile.Has(Capability.MULTIPLE_IMAGE_INPUT), Is.True);
            Assert.That(profile.Has(Capability.FUNCTION_CALLING), Is.True);
            Assert.That(profile.Has(Capability.WEB_SEARCH), Is.True);
            Assert.That(profile.Context.IsKnown, Is.True);
            Assert.That(profile.Context.DefaultTokens, Is.EqualTo(1_050_000));
        });
    }

    [TestCase("gpt-6-astra", ReasoningSupport.ALWAYS, TestName = "Astra reasons on every request")]
    [TestCase("gpt-6-sol", ReasoningSupport.ON_BY_DEFAULT, TestName = "Sol reasons unless the effort is set to none")]
    [TestCase("gpt-6-luna", ReasoningSupport.ON_BY_DEFAULT, TestName = "Luna reasons unless the effort is set to none")]
    public void EveryGpt6ModelReasonsTheWayItsModelPageSays(string modelId, ReasoningSupport reasoning)
    {
        var profile = LLMProviders.OPEN_AI.GetModelProfile(new Model(modelId, null));

        Assert.That(profile.Reasoning, Is.EqualTo(reasoning));
    }
}