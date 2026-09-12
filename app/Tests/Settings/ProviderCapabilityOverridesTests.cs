using AIStudio.Models;
using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks what a person's own settings do to what the rules worked out.
/// </summary>
/// <remarks>
/// The expert dialog writes the three reasoning words together, in five combinations. Those five
/// are the whole surface the app produces, so they are the ones held against the code being
/// replaced: every one of them has to come out of the new resolution exactly as it comes out of the
/// old repair today.
///
/// A configuration plugin can write the three words one at a time, and there the two differ on
/// purpose. The old repair took a word away unless another one stood next to it, so an override
/// about something else destroyed an answer nobody had touched. Those cases are stated below, one
/// by one, with what they answer now.
/// </remarks>
[TestFixture]
public sealed class ProviderCapabilityOverridesTests
{
    private static readonly ReasoningSupport[] EVERY_STATE = [ReasoningSupport.NONE, ReasoningSupport.OPTIONAL, ReasoningSupport.ON_BY_DEFAULT, ReasoningSupport.ALWAYS];

    /// <summary>
    /// The five combinations the expert dialog writes, in the order its list shows them.
    /// </summary>
    /// <remarks>
    /// "Automatic" is not among them. It means the person said nothing, and a provider carrying
    /// nothing but nothing is saved without an override record at all, so it never reaches here --
    /// which is exactly why the defect below went unnoticed for so long: it needed a second,
    /// unrelated switch to become visible.
    /// </remarks>
    private static readonly ProviderCapabilityOverrides[] WHAT_THE_DIALOG_WRITES =
    [
        new() { AlwaysReasoning = false, OptionalReasoning = false, ReasoningByDefault = false },
        new() { AlwaysReasoning = false, OptionalReasoning = true, ReasoningByDefault = false },
        new() { AlwaysReasoning = false, OptionalReasoning = true, ReasoningByDefault = true },
        new() { AlwaysReasoning = true, OptionalReasoning = false, ReasoningByDefault = false },
    ];

    [Test]
    public void EveryChoiceTheExpertDialogOffersMeansTheSameAsItDoesToday()
    {
        Assert.Multiple(() =>
        {
            foreach (var overrides in WHAT_THE_DIALOG_WRITES)
            foreach (var stated in EVERY_STATE)
            {
                var rebuilt = overrides.ApplyTo(ProfileWhichReasons(stated)).Reasoning;
                var today = ReasoningOf(overrides.ApplyTo(CapabilitiesWhichReason(stated)));

                Assert.That(rebuilt, Is.EqualTo(today), $"A model which reasons {stated}, with {Describe(overrides)}.");
            }
        });
    }

    [Test]
    public void AnOverrideAboutSomethingElseNoLongerTakesTheThinkingAway()
    {
        //
        // The defect this replaces. Turning tool calling off said nothing about reasoning, and yet
        // a model which thinks unless asked not to came out of it as a model which never thinks --
        // because the repair kept "on by default" only where "on request" stood next to it, which
        // no rule has ever stated.
        //
        var overrides = new ProviderCapabilityOverrides { FunctionCalling = false };
        var thinker = ProfileWhichReasons(ReasoningSupport.ON_BY_DEFAULT);

        Assert.Multiple(() =>
        {
            Assert.That(overrides.ApplyTo(thinker).Reasoning, Is.EqualTo(ReasoningSupport.ON_BY_DEFAULT));
            Assert.That(ReasoningOf(overrides.ApplyTo(CapabilitiesWhichReason(ReasoningSupport.ON_BY_DEFAULT))), Is.EqualTo(ReasoningSupport.NONE), "Which is what it used to answer, and the reason this test exists.");
        });
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

    /// <summary>
    /// The same model, written the way the rules being replaced answer.
    /// </summary>
    /// <param name="reasoning">How the model reasons.</param>
    /// <returns>The capabilities, with the one word which stands for that state.</returns>
    private static List<Capability> CapabilitiesWhichReason(ReasoningSupport reasoning)
    {
        List<Capability> capabilities = [Capability.TEXT_INPUT, Capability.TEXT_OUTPUT];
        switch (reasoning)
        {
            case ReasoningSupport.OPTIONAL:
                capabilities.Add(Capability.OPTIONAL_REASONING);
                break;

            case ReasoningSupport.ON_BY_DEFAULT:
                capabilities.Add(Capability.REASONING_BY_DEFAULT);
                break;

            case ReasoningSupport.ALWAYS:
                capabilities.Add(Capability.ALWAYS_REASONING);
                break;
        }

        return capabilities;
    }

    /// <summary>
    /// Which state a list of capabilities stands for, read the way the app reads it today.
    /// </summary>
    /// <param name="capabilities">The capabilities.</param>
    /// <returns>The state they stand for.</returns>
    private static ReasoningSupport ReasoningOf(List<Capability> capabilities)
    {
        if (capabilities.Contains(Capability.ALWAYS_REASONING))
            return ReasoningSupport.ALWAYS;

        if (capabilities.Contains(Capability.REASONING_BY_DEFAULT))
            return ReasoningSupport.ON_BY_DEFAULT;

        if (capabilities.Contains(Capability.OPTIONAL_REASONING))
            return ReasoningSupport.OPTIONAL;

        return ReasoningSupport.NONE;
    }

    private static string Describe(ProviderCapabilityOverrides overrides) => $"always={overrides.AlwaysReasoning?.ToString() ?? "auto"}, optional={overrides.OptionalReasoning?.ToString() ?? "auto"}, byDefault={overrides.ReasoningByDefault?.ToString() ?? "auto"}";
}