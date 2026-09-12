using AIStudio.Provider;
using AIStudio.Provider.Reasoning;

using Host = AIStudio.Provider.SelfHosted.Host;

namespace AIStudio.Tests.Provider.Reasoning;

/// <summary>
/// Checks what the app makes of the API parameters a person wrote themselves.
/// </summary>
/// <remarks>
/// This is the first test this reading has ever had. Five hundred lines interpreted a dozen ways of
/// saying "think" across nine providers and three engines, and the only way to find out whether any
/// of it was right was to configure a provider and watch an icon.
///
/// The parameters are stored the way the settings dialog stores them: the body of a JSON object,
/// without the braces around it. That is why every fragment below starts with a quoted key.
/// </remarks>
[TestFixture]
public sealed class ReasoningDispatcherTests
{
    [TestCase(LLMProviders.OPEN_AI, """ "reasoning_effort": "high" """, ReasoningConfigurationState.EXPLICITLY_ENABLED)]
    [TestCase(LLMProviders.OPEN_AI, """ "reasoning": { "effort": "none" } """, ReasoningConfigurationState.EXPLICITLY_DISABLED)]
    [TestCase(LLMProviders.OPEN_AI, """ "reasoning": { } """, ReasoningConfigurationState.NOT_CONFIGURED, Description = "An empty object is somebody who has not asked for anything yet.")]
    [TestCase(LLMProviders.OPEN_AI, """ "temperature": 0.5 """, ReasoningConfigurationState.NOT_CONFIGURED)]
    [TestCase(LLMProviders.ANTHROPIC, """ "thinking": { "type": "enabled" } """, ReasoningConfigurationState.EXPLICITLY_ENABLED)]
    [TestCase(LLMProviders.ANTHROPIC, """ "thinking": { "type": "adaptive" } """, ReasoningConfigurationState.EXPLICITLY_ENABLED)]
    [TestCase(LLMProviders.ANTHROPIC, """ "thinking": { "type": "disabled" } """, ReasoningConfigurationState.EXPLICITLY_DISABLED)]
    [TestCase(LLMProviders.GOOGLE, """ "thinking_config": { "thinking_budget": 0 } """, ReasoningConfigurationState.EXPLICITLY_DISABLED, Description = "A budget of nothing is the way Google switches thinking off.")]
    [TestCase(LLMProviders.GOOGLE, """ "generation_config": { "thinking_config": { "thinkingBudget": 1024 } } """, ReasoningConfigurationState.EXPLICITLY_ENABLED, Description = "Nested, and in the other spelling their own libraries write.")]
    [TestCase(LLMProviders.GOOGLE, """ "thinking_summaries": "auto" """, ReasoningConfigurationState.EXPLICITLY_ENABLED)]
    [TestCase(LLMProviders.GOOGLE, """ "thinking_summaries": "off" """, ReasoningConfigurationState.NOT_CONFIGURED, Description = "A model can think without showing it, so switching summaries off proves nothing.")]
    [TestCase(LLMProviders.GOOGLE, """ "reasoning_effort": "minimal" """, ReasoningConfigurationState.EXPLICITLY_ENABLED, Description = "Google's OpenAI-compatible endpoint takes the effort too, which the table has to say out loud now that the dialect no longer smuggles it in.")]
    [TestCase(LLMProviders.ALIBABA_CLOUD, """ "enable_thinking": false """, ReasoningConfigurationState.EXPLICITLY_DISABLED)]
    [TestCase(LLMProviders.GROQ, """ "chat_template_kwargs": { "enable_thinking": true } """, ReasoningConfigurationState.EXPLICITLY_ENABLED, Description = "A gateway serves everybody's models, so it is asked in everybody's dialect.")]
    public void TheParametersOfAProviderAreReadInTheDialectsItSpeaks(LLMProviders provider, string parameters, ReasoningConfigurationState wanted)
    {
        Assert.That(ReasoningDispatcher.WhatTheParametersSay(provider, Host.NONE, parameters), Is.EqualTo(wanted));
    }

    [TestCase(Host.OLLAMA, """ "think": true """, ReasoningConfigurationState.EXPLICITLY_ENABLED)]
    [TestCase(Host.OLLAMA, """ "think": "off" """, ReasoningConfigurationState.EXPLICITLY_DISABLED)]
    [TestCase(Host.LLAMA_CPP, """ "reasoning": "on" """, ReasoningConfigurationState.EXPLICITLY_ENABLED)]
    [TestCase(Host.LLAMA_CPP, """ "reasoning": "off" """, ReasoningConfigurationState.EXPLICITLY_DISABLED)]
    [TestCase(Host.LLAMA_CPP, """ "reasoning": "auto" """, ReasoningConfigurationState.NOT_CONFIGURED, Description = "Auto hands the decision to the model's own template, which means nobody decided.")]
    [TestCase(Host.LLAMA_CPP, """ "reasoning_budget": 0 """, ReasoningConfigurationState.EXPLICITLY_DISABLED)]
    [TestCase(Host.VLLM, """ "thinking_token_budget": 2048 """, ReasoningConfigurationState.EXPLICITLY_ENABLED)]
    [TestCase(Host.VLLM, """ "chat_template_kwargs": { "thinking": true } """, ReasoningConfigurationState.EXPLICITLY_ENABLED)]
    public void EachSelfHostedEngineIsReadInItsOwn(Host host, string parameters, ReasoningConfigurationState wanted)
    {
        Assert.That(ReasoningDispatcher.WhatTheParametersSay(LLMProviders.SELF_HOSTED, host, parameters), Is.EqualTo(wanted));
    }

    [Test]
    public void AProviderIsNotReadInADialectItDoesNotSpeak()
    {
        //
        // The reason the table exists. Mistral accepts an effort and nothing else, so writing Qwen's
        // switch into a Mistral provider says nothing -- and claiming it did would light an indicator
        // for a request which will never carry that parameter anywhere.
        //
        Assert.Multiple(() =>
        {
            Assert.That(ReasoningDispatcher.WhatTheParametersSay(LLMProviders.MISTRAL, Host.NONE, """ "enable_thinking": true """), Is.EqualTo(ReasoningConfigurationState.NOT_CONFIGURED));
            Assert.That(ReasoningDispatcher.WhatTheParametersSay(LLMProviders.ANTHROPIC, Host.NONE, """ "reasoning_effort": "high" """), Is.EqualTo(ReasoningConfigurationState.NOT_CONFIGURED));
            Assert.That(ReasoningDispatcher.WhatTheParametersSay(LLMProviders.MISTRAL, Host.NONE, """ "reasoning_effort": "high" """), Is.EqualTo(ReasoningConfigurationState.EXPLICITLY_ENABLED), "And the one it does speak still counts.");
        });
    }

    [Test]
    public void ANoWinsOverAYesWhereverTheTwoStand()
    {
        //
        // Somebody who switched thinking off in one place meant to switch it off. An indicator
        // lighting up because another parameter could be read as a yes would be the app arguing
        // with them about their own settings.
        //
        var state = ReasoningDispatcher.WhatTheParametersSay(LLMProviders.SELF_HOSTED, Host.OLLAMA, """ "think": true, "enable_thinking": false """);

        Assert.That(state, Is.EqualTo(ReasoningConfigurationState.EXPLICITLY_DISABLED));
    }

    [TestCase("", Description = "Nothing configured at all.")]
    [TestCase("   ")]
    [TestCase(""" "reasoning_effort": """, Description = "A fragment somebody is still typing.")]
    [TestCase("not json at all")]
    public void ParametersNobodyCanReadSayNothing(string parameters)
    {
        Assert.That(ReasoningDispatcher.WhatTheParametersSay(LLMProviders.OPEN_AI, Host.NONE, parameters), Is.EqualTo(ReasoningConfigurationState.NOT_CONFIGURED));
    }

    [Test]
    public void WithoutAProviderNothingIsRead()
    {
        Assert.That(ReasoningDispatcher.WhatTheParametersSay(LLMProviders.NONE, Host.NONE, """ "reasoning_effort": "high" """), Is.EqualTo(ReasoningConfigurationState.NOT_CONFIGURED));
    }

    [Test]
    public void EveryDialectThereIsCanBeAsked()
    {
        //
        // Adding a way of saying "think" means adding a member to the enum and a class next to it.
        // Forgetting the second half would make the first half a name nothing answers to, and the
        // provider naming it in its table would quietly read one dialect less.
        //
        var registered = ReasoningDispatcher.Dialects.Select(dialect => dialect.Dialect).ToList();

        Assert.That(registered, Is.EquivalentTo(Enum.GetValues<ReasoningDialect>()));
    }

    [Test]
    public void EveryDialectAProviderNamesIsOneThatExists()
    {
        var known = ReasoningDispatcher.Dialects.Select(dialect => dialect.Dialect).ToHashSet();

        Assert.Multiple(() =>
        {
            foreach (var provider in Enum.GetValues<LLMProviders>())
            foreach (var host in Enum.GetValues<Host>())
            {
                var named = ReasoningDispatcher.DialectsOf(provider, host);

                Assert.That(named, Is.SubsetOf(known), $"{provider} on {host} names a dialect nothing answers to.");
                Assert.That(named, Is.Unique, $"{provider} on {host} names a dialect twice.");
            }
        });
    }
}