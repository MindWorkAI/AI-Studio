using AIStudio.Models;
using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks what a person's own settings do to what the rules worked out.
/// </summary>
/// <remarks>
/// The expert dialog writes the three reasoning words together, in five combinations. Those five
/// are the whole surface the app produces, so each of them is stated below with the one thing it
/// means -- whatever the rules said about the model, because that is what choosing from a list of
/// five does.
///
/// These used to be measured against the repair they replaced, by running both and comparing. That
/// comparison is gone with the repair itself: keeping a dead implementation alive so a test can ask
/// it questions makes the test the only reason it still exists, and the next reader cannot tell
/// which of the two is the real one. What it guaranteed is written out instead.
///
/// A configuration plugin can write the three words one at a time, and there the two differed on
/// purpose. The repair took a word away unless another one stood next to it, so an override about
/// something else destroyed an answer nobody had touched. Those cases are stated below, one by one,
/// with what they answer now.
/// </remarks>
[TestFixture]
public sealed class ProviderCapabilityOverridesTests
{
    private static readonly ReasoningSupport[] EVERY_STATE = [ReasoningSupport.NONE, ReasoningSupport.OPTIONAL, ReasoningSupport.ON_BY_DEFAULT, ReasoningSupport.ALWAYS];

    /// <summary>
    /// The four combinations the expert dialog writes, in the order its list shows them, and the
    /// one state each of them means.
    /// </summary>
    /// <remarks>
    /// "Automatic" is the fifth choice and is not among them. It means the person said nothing, and
    /// a provider carrying nothing but nothing is saved without an override record at all, so it
    /// never reaches here -- which is exactly why the defect below went unnoticed for so long: it
    /// needed a second, unrelated switch to become visible.
    /// </remarks>
    private static readonly (ProviderCapabilityOverrides Overrides, ReasoningSupport Means)[] WHAT_THE_DIALOG_WRITES =
    [
        (new() { AlwaysReasoning = false, OptionalReasoning = false, ReasoningByDefault = false }, ReasoningSupport.NONE),
        (new() { AlwaysReasoning = false, OptionalReasoning = true, ReasoningByDefault = false }, ReasoningSupport.OPTIONAL),
        (new() { AlwaysReasoning = false, OptionalReasoning = true, ReasoningByDefault = true }, ReasoningSupport.ON_BY_DEFAULT),
        (new() { AlwaysReasoning = true, OptionalReasoning = false, ReasoningByDefault = false }, ReasoningSupport.ALWAYS),
    ];

    [Test]
    public void EveryChoiceTheExpertDialogOffersMeansOneStateAndNothingElse()
    {
        //
        // Whatever the rules said about the model is beside the point here: somebody picked one of
        // five entries from a list, and each entry says outright how this model reasons. That is
        // also what makes these four the cheapest guard there is against somebody rearranging the
        // resolution below them.
        //
        Assert.Multiple(() =>
        {
            foreach (var (overrides, means) in WHAT_THE_DIALOG_WRITES)
            foreach (var stated in EVERY_STATE)
                Assert.That(overrides.ApplyTo(ProfileWhichReasons(stated)).Reasoning, Is.EqualTo(means), $"A model which reasons {stated}, with {Describe(overrides)}.");
        });
    }

    [Test]
    public void AnOverrideAboutSomethingElseLeavesTheThinkingAlone()
    {
        //
        // The defect this replaced. Turning tool calling off said nothing about reasoning, and yet
        // a model which thinks unless asked not to came out of it as a model which never thinks --
        // because the repair kept "on by default" only where "on request" stood next to it, which
        // no rule has ever stated. It cannot come back through this door: the answer is one value
        // now, and the combination the repair existed for cannot be written down any more.
        //
        var overrides = new ProviderCapabilityOverrides { FunctionCalling = false };

        Assert.That(overrides.ApplyTo(ProfileWhichReasons(ReasoningSupport.ON_BY_DEFAULT)).Reasoning, Is.EqualTo(ReasoningSupport.ON_BY_DEFAULT));
    }

    [TestCase(ReasoningSupport.ALWAYS, ReasoningSupport.ALWAYS, Description = "Saying it is not on by default says nothing about a model which cannot turn it off.")]
    [TestCase(ReasoningSupport.ON_BY_DEFAULT, ReasoningSupport.NONE, Description = "Here it names the state the model is in.")]
    [TestCase(ReasoningSupport.OPTIONAL, ReasoningSupport.OPTIONAL, Description = "A model which reasons on request was never on by default.")]
    public void ANoOnlyTakesAwayTheStateItNames(ReasoningSupport stated, ReasoningSupport wanted)
    {
        var overrides = new ProviderCapabilityOverrides { ReasoningByDefault = false };

        Assert.That(overrides.ApplyTo(ProfileWhichReasons(stated)).Reasoning, Is.EqualTo(wanted));
    }

    [Test]
    public void AYesIsTheWholeAnswer()
    {
        var alwaysOn = new ProviderCapabilityOverrides { AlwaysReasoning = true, OptionalReasoning = false, ReasoningByDefault = false };

        Assert.That(alwaysOn.ApplyTo(ProfileWhichReasons(ReasoningSupport.NONE)).Reasoning, Is.EqualTo(ReasoningSupport.ALWAYS));
    }

    [Test]
    public void SwitchingACapabilityOnAndOffTouchesNothingElse()
    {
        var profile = new ModelProfile
        {
            Capabilities = Capability.TEXT_INPUT | Capability.TEXT_OUTPUT | Capability.SINGLE_IMAGE_INPUT | Capability.FUNCTION_CALLING,
            Kind = ModelKind.CHAT,
            Context = ContextWindow.Of(128_000),
        };

        var overrides = new ProviderCapabilityOverrides { FunctionCalling = false, AudioInput = true };
        var after = overrides.ApplyTo(profile);

        Assert.Multiple(() =>
        {
            Assert.That(after.Has(Capability.FUNCTION_CALLING), Is.False);
            Assert.That(after.Has(Capability.AUDIO_INPUT), Is.True);
            Assert.That(after.Has(Capability.TEXT_INPUT), Is.True);
            Assert.That(after.Has(Capability.SINGLE_IMAGE_INPUT), Is.True, "Turning several images off is what removes several images; one image is a statement of its own.");
            Assert.That(after.Context, Is.EqualTo(profile.Context));
        });
    }

    /// <summary>
    /// A profile which reasons the given way and says nothing else.
    /// </summary>
    /// <param name="reasoning">How the model reasons.</param>
    /// <returns>The profile.</returns>
    private static ModelProfile ProfileWhichReasons(ReasoningSupport reasoning) => new()
    {
        Capabilities = Capability.TEXT_INPUT | Capability.TEXT_OUTPUT,
        Reasoning = reasoning,
    };

    private static string Describe(ProviderCapabilityOverrides overrides) => $"always={overrides.AlwaysReasoning?.ToString() ?? "auto"}, optional={overrides.OptionalReasoning?.ToString() ?? "auto"}, byDefault={overrides.ReasoningByDefault?.ToString() ?? "auto"}";
}