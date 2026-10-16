using AIStudio.Models;
using AIStudio.Models.Hosting;
using AIStudio.Models.Matching;
using AIStudio.Provider;

namespace AIStudio.Tests.Models.Hosting;

/// <summary>
/// Checks the walk which takes a name apart, and what happens when nobody wrote a host.
/// </summary>
/// <remarks>
/// How deep a wrapping goes is the host's business, not the caller's: Hugging Face has two, Fireworks
/// has three, most have none. Asking over and over until the host says no is what covers all of
/// them, and what has to be bounded so that a host which never says no cannot hang the app.
/// </remarks>
[TestFixture]
public sealed class ModelHostIndexTests
{
    [Test]
    public void TheHostsAreKeptInTheOrderOfTheProvidersTheyAnswerFor()
    {
        var index = ModelHostIndex.Build([new SplittingHost(), new StubbornHost()]);

        Assert.That(index.Hosts.Select(host => host.Provider), Is.EqualTo(new[] { LLMProviders.OPEN_ROUTER, LLMProviders.LITE_LLM }));
    }

    [Test]
    public void TwoHostsForOneProviderIsRefused()
    {
        var refused = Assert.Throws<InvalidOperationException>(() => ModelHostIndex.Build([new SplittingHost(), new SecondHostForTheSameProvider()]));

        Assert.That(refused?.Message, Does.Contain("OPEN_ROUTER"));
    }

    [Test]
    public void AHostAnsweringForNoProviderIsRefused()
    {
        //
        // The default value of the provider enum is NONE, so a host which gets this wrong gets it
        // wrong quietly: it would sit in the index answering for a provider nobody can configure.
        //
        var refused = Assert.Throws<InvalidOperationException>(() => ModelHostIndex.Build([new HostForNobody()]));

        Assert.That(refused?.Message, Does.Contain(nameof(HostForNobody)));
    }

    [Test]
    public void ProvidersNobodyWroteAHostForAreNamed()
    {
        var index = ModelHostIndex.Build([new SplittingHost()]);

        Assert.Multiple(() =>
        {
            Assert.That(index.ProvidersWithoutAHost, Does.Contain(LLMProviders.ANTHROPIC));
            Assert.That(index.ProvidersWithoutAHost, Does.Not.Contain(LLMProviders.OPEN_ROUTER));
            Assert.That(index.ProvidersWithoutAHost, Does.Not.Contain(LLMProviders.NONE), "Nobody can configure it, so nobody has to write a host for it.");
        });
    }

    [Test]
    public void AProviderWithoutAHostGetsItsNameBackUntouched()
    {
        var index = ModelHostIndex.Build([new SplittingHost()]);
        var unwrapped = index.Unwrap(new ModelId("anthropic/claude-opus-5"), LLMProviders.ANTHROPIC, out var vendor);

        Assert.Multiple(() =>
        {
            Assert.That(index.Of(LLMProviders.ANTHROPIC), Is.Null);
            Assert.That(unwrapped.Original, Is.EqualTo("anthropic/claude-opus-5"));
            Assert.That(vendor, Is.Null);
        });
    }

    [Test]
    public void AProviderWithoutAHostStillLosesTheResponsesApi()
    {
        //
        // The safe direction: claiming an API which is not there turns into a failed request, while
        // not claiming one only means the app does not use it.
        //
        var index = ModelHostIndex.Build([new SplittingHost()]);
        var profile = new ModelProfile { Capabilities = Capability.TEXT_INPUT | Capability.RESPONSES_API };
        var throughTheProvider = index.ApplyTransport(profile, LLMProviders.ANTHROPIC);

        Assert.Multiple(() =>
        {
            Assert.That(throughTheProvider.Has(Capability.RESPONSES_API), Is.False);
            Assert.That(throughTheProvider.Has(Capability.CHAT_COMPLETION_API), Is.True);
        });
    }

    [Test]
    public void TheWalkKeepsAskingUntilTheHostSaysNo()
    {
        var index = ModelHostIndex.Build([new SplittingHost()]);
        var unwrapped = index.Unwrap(new ModelId("accounts/fireworks/models/llama-v3p1-405b-instruct"), LLMProviders.OPEN_ROUTER, out _);

        Assert.That(unwrapped.Original, Is.EqualTo("llama-v3p1-405b-instruct"));
    }

    [Test]
    public void TheInnermostWrappingIsTheOneWhichSaysWhoBuiltTheModel()
    {
        var index = ModelHostIndex.Build([new SplittingHost()]);
        index.Unwrap(new ModelId("anthropic/openai/gpt-5"), LLMProviders.OPEN_ROUTER, out var vendor);

        Assert.That(vendor, Is.EqualTo(ModelVendor.OPEN_AI), "A wrapping closer to the model knows more about it than one further out.");
    }

    [Test]
    public void AWrappingWhichSaysNothingDoesNotEraseWhatAnOuterOneSaid()
    {
        var index = ModelHostIndex.Build([new SplittingHost()]);
        index.Unwrap(new ModelId("anthropic/somebody-else/claude-opus-5"), LLMProviders.OPEN_ROUTER, out var vendor);

        Assert.That(vendor, Is.EqualTo(ModelVendor.ANTHROPIC));
    }

    [Test]
    public void AHostHandingBackWhatItWasGivenIsNotAskedAgain()
    {
        var index = ModelHostIndex.Build([new StubbornHost()]);
        var unwrapped = index.Unwrap(new ModelId("the-fast-one"), LLMProviders.LITE_LLM, out _);

        Assert.That(unwrapped.Original, Is.EqualTo("the-fast-one"));
    }

    [Test]
    public void AHostWhichNeverSaysNoIsStoppedRatherThanFollowedForever()
    {
        var index = ModelHostIndex.Build([new GrowingHost()]);
        var unwrapped = index.Unwrap(new ModelId("thing"), LLMProviders.GROQ, out _);

        Assert.That(unwrapped.Original.Split("-more"), Has.Length.EqualTo(ModelHostIndex.MAX_UNWRAPPING_STEPS + 1));
    }

    private sealed class SplittingHost : ModelHost
    {
        public override LLMProviders Provider => LLMProviders.OPEN_ROUTER;

        public override ModelSource Source => new("https://example.invalid/splitting", new DateOnly(2026, 9, 11), "A host taking off one organization at a time.");

        public override bool TryUnwrap(in ModelId id, out ModelId inner, out ModelVendor? declaredVendor) => HostNaming.TrySplitOrganization(id, out inner, out declaredVendor);
    }

    private sealed class SecondHostForTheSameProvider : ModelHost
    {
        public override LLMProviders Provider => LLMProviders.OPEN_ROUTER;

        public override ModelSource Source => new("https://example.invalid/second", new DateOnly(2026, 9, 11), "A second host claiming a provider which already has one.");
    }

    private sealed class HostForNobody : ModelHost
    {
        public override LLMProviders Provider => LLMProviders.NONE;

        public override ModelSource Source => new("https://example.invalid/nobody", new DateOnly(2026, 9, 11), "A host which names no provider.");
    }

    private sealed class StubbornHost : ModelHost
    {
        public override LLMProviders Provider => LLMProviders.LITE_LLM;

        public override ModelSource Source => new("https://example.invalid/stubborn", new DateOnly(2026, 9, 11), "A host saying it unwrapped something without shortening anything.");

        public override bool TryUnwrap(in ModelId id, out ModelId inner, out ModelVendor? declaredVendor)
        {
            inner = id;
            declaredVendor = null;
            return true;
        }
    }

    private sealed class GrowingHost : ModelHost
    {
        public override LLMProviders Provider => LLMProviders.GROQ;

        public override ModelSource Source => new("https://example.invalid/growing", new DateOnly(2026, 9, 11), "A host handing back a longer name every time it is asked.");

        public override bool TryUnwrap(in ModelId id, out ModelId inner, out ModelVendor? declaredVendor)
        {
            inner = new($"{id.Original}-more");
            declaredVendor = null;
            return true;
        }
    }
}