using AIStudio.Models;
using AIStudio.Models.Matching;
using AIStudio.Models.Plugins;
using AIStudio.Models.Registry;
using AIStudio.Provider;

namespace AIStudio.Tests.Models.Plugins;

/// <summary>
/// Checks where what an organization declares stands against what AI Studio works out itself.
/// </summary>
/// <remarks>
/// Each test builds a registry of its own rather than asking the one the app uses. What is being
/// checked is the order of the chain, and a test which had to name a real model to check it would
/// start failing the day somebody corrects that model's rule.
///
/// The one exception borrows the registry the app uses, because only that one knows the hosts. It
/// hands it back empty, and the fixture is kept out of any parallel run so that the borrowing
/// cannot reach a test asking the same registry about a real model.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class DeclaredModelsTests
{
    private static readonly Guid PLUGIN_ID = new("11111111-1111-1111-1111-111111111111");

    [Test]
    public void WhatAnOrganizationDeclaresComesBeforeWhatTheRulesWorkOut()
    {
        var registry = ModelRegistry.Build([new AcmeFamily()], []);
        registry.Declare([Declaring("acme-assistant", MatchKind.PREFIX, Capability.TEXT_INPUT | Capability.TEXT_OUTPUT | Capability.MULTIPLE_IMAGE_INPUT)]);

        var profile = registry.Profile(LLMProviders.SELF_HOSTED, "acme-assistant-7b");

        Assert.That(profile.Has(Capability.MULTIPLE_IMAGE_INPUT), Is.True, "Only the organization says this model reads images, and they are the ones running it.");
    }

    [Test]
    public void ADeclarationIsTheWholeStatementAndNotAnAdditionToOne()
    {
        //
        // The part an administrator has to be able to rely on. Their entry says what the model can
        // do, so what AI Studio would have said instead is gone -- including the capabilities their
        // entry does not mention. Adding to the built-in answer would make it impossible to take
        // anything away, which is exactly what somebody correcting us is trying to do.
        //
        var registry = ModelRegistry.Build([new AcmeFamily()], []);
        registry.Declare([Declaring("acme-assistant", MatchKind.PREFIX, Capability.TEXT_INPUT | Capability.TEXT_OUTPUT)]);

        var profile = registry.Profile(LLMProviders.SELF_HOSTED, "acme-assistant-7b");

        Assert.Multiple(() =>
        {
            Assert.That(profile.Has(Capability.FUNCTION_CALLING), Is.False, "The built-in rule grants this one, and the declaration does not.");
            Assert.That(profile.Reasoning, Is.EqualTo(ReasoningSupport.NONE), "Nor does it reason, whatever the built-in rule says.");
        });
    }

    [Test]
    public void AModelNoDeclarationMentionsIsAnsweredByTheRulesAsBefore()
    {
        var registry = ModelRegistry.Build([new AcmeFamily()], []);
        registry.Declare([Declaring("something-else", MatchKind.SEGMENT, Capability.TEXT_INPUT)]);

        var profile = registry.Profile(LLMProviders.SELF_HOSTED, "acme-assistant-7b");

        Assert.That(profile.Has(Capability.FUNCTION_CALLING), Is.True);
    }

    [Test]
    public void TakingADeclarationAwayBringsTheBuiltInAnswerBack()
    {
        //
        // This is what happens when an organization withdraws a configuration, or when somebody
        // corrects their plugin and the plugins are reloaded. It is also the test that the kept
        // answers are dropped along with the declarations they were worked out under: a cache which
        // outlived them would go on answering with what a plugin said which is no longer there.
        //
        var registry = ModelRegistry.Build([new AcmeFamily()], []);
        registry.Declare([Declaring("acme-assistant", MatchKind.PREFIX, Capability.TEXT_INPUT | Capability.TEXT_OUTPUT)]);

        var whileDeclared = registry.Profile(LLMProviders.SELF_HOSTED, "acme-assistant-7b");
        registry.Declare([]);
        var afterwards = registry.Profile(LLMProviders.SELF_HOSTED, "acme-assistant-7b");

        Assert.Multiple(() =>
        {
            Assert.That(whileDeclared.Has(Capability.FUNCTION_CALLING), Is.False);
            Assert.That(afterwards.Has(Capability.FUNCTION_CALLING), Is.True);
        });
    }

    [Test]
    public void AmongTheDeclarationsTheOneSayingMoreAboutTheNameWins()
    {
        //
        // Two plugins, or one plugin describing a family and then one of its variants. Nothing new
        // is needed for this: the declarations go through the same engine as the built-in rules, so
        // the specificity is computed here too and nobody writes an order.
        //
        var registry = ModelRegistry.Build([new AcmeFamily()], []);
        registry.Declare(
        [
            Declaring("acme-assistant", MatchKind.PREFIX, Capability.TEXT_INPUT | Capability.TEXT_OUTPUT),
            Declaring("acme-assistant-7b", MatchKind.EXACT, Capability.TEXT_INPUT | Capability.TEXT_OUTPUT | Capability.MULTIPLE_IMAGE_INPUT),
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(registry.Profile(LLMProviders.SELF_HOSTED, "acme-assistant-7b").Has(Capability.MULTIPLE_IMAGE_INPUT), Is.True);
            Assert.That(registry.Profile(LLMProviders.SELF_HOSTED, "acme-assistant-3b").Has(Capability.MULTIPLE_IMAGE_INPUT), Is.False);
        });
    }

    [Test]
    public void ADeclarationIsMeasuredAgainstTheNameWithoutTheProvidersWrapping()
    {
        //
        // An administrator writes the model's name, not the name plus whatever the gateway they
        // reach it through puts in front of it. Unwrapping happens before anything is asked, so the
        // same entry answers whichever way the model is reached.
        //
        var registry = ModelRegistry.Shared;
        var declaration = Declaring("gpt-5.1", MatchKind.PREFIX, Capability.TEXT_INPUT | Capability.TEXT_OUTPUT | Capability.VIDEO_INPUT);

        try
        {
            registry.Declare([declaration]);

            Assert.Multiple(() =>
            {
                Assert.That(registry.Profile(LLMProviders.OPEN_AI, "gpt-5.1").Has(Capability.VIDEO_INPUT), Is.True);
                Assert.That(registry.Profile(LLMProviders.OPEN_ROUTER, "openai/gpt-5.1").Has(Capability.VIDEO_INPUT), Is.True, "The same model, reached through a gateway which wraps the name.");
            });
        }
        finally
        {
            //
            // The registry the app uses is the only one which knows the hosts, so this test has to
            // borrow it. Handing it back empty is what keeps the borrowing from reaching the tests
            // which ask it about real models.
            //
            registry.Declare([]);
        }
    }

    private static ModelDeclaration Declaring(string pattern, MatchKind matchKind, Capability capabilities) => new()
    {
        Pattern = new()
        {
            Kind = matchKind,
            Text = pattern,
        },

        Change = new()
        {
            Adds = capabilities,
        },

        Source = new("https://intranet.invalid/ai", new DateOnly(2026, 9, 12), "What a company says about its own models."),
        Origin = "Models of a company",
        EnterpriseConfigurationPluginId = PLUGIN_ID,
    };

    /// <summary>
    /// A family which says more about these models than the declarations of this test do.
    /// </summary>
    private sealed class AcmeFamily : ModelFamily
    {
        public override ModelVendor Vendor => ModelVendor.UNKNOWN;

        public override ModelSource Source => new("https://example.invalid/acme", new DateOnly(2026, 9, 12), "A family standing in for whatever AI Studio knows by itself.");

        protected override void Declare(ModelFamilyBuilder builder) =>
            builder.Rule("acme-assistant").AsPrefix()
                .Capabilities(Capability.TEXT_INPUT | Capability.TEXT_OUTPUT | Capability.FUNCTION_CALLING)
                .Apis(Capability.CHAT_COMPLETION_API)
                .Reasoning(ReasoningSupport.ALWAYS);
    }
}