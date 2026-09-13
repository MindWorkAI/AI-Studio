using AIStudio.Models;
using AIStudio.Provider;

namespace AIStudio.Tests.Models;

/// <summary>
/// Checks the answer object itself: what it says, and what it refuses to say.
/// </summary>
[TestFixture]
public sealed class ModelProfileTests
{
    [Test]
    public void AProfileNobodyWroteAnythingIntoKnowsNothingAndStillCountsAsAChatModel()
    {
        var untouched = ModelProfile.UNKNOWN;

        Assert.Multiple(() =>
        {
            Assert.That(untouched.Capabilities, Is.EqualTo(Capability.NONE));
            Assert.That(untouched.Reasoning, Is.EqualTo(ReasoningSupport.NONE));
            Assert.That(untouched.Context.IsKnown, Is.False);

            //
            // A model we fail to recognize has to stay visible to the user rather than disappear
            // from their list, which is why the unrecognized kind is chat rather than something
            // meaning "no idea".
            //
            Assert.That(untouched.Kind, Is.EqualTo(ModelKind.CHAT));
        });
    }

    [Test]
    public void AskingWhetherAModelHasSeveralCapabilitiesAsksForAllOfThem()
    {
        var profile = new ModelProfile { Capabilities = Capability.TEXT_INPUT | Capability.TEXT_OUTPUT };

        Assert.Multiple(() =>
        {
            Assert.That(profile.Has(Capability.TEXT_INPUT | Capability.TEXT_OUTPUT), Is.True);
            Assert.That(profile.Has(Capability.TEXT_INPUT | Capability.WEB_SEARCH), Is.False);
            Assert.That(profile.HasAny(Capability.TEXT_INPUT | Capability.WEB_SEARCH), Is.True);
            Assert.That(profile.HasAny(Capability.WEB_SEARCH | Capability.EMBEDDING), Is.False);
        });
    }

    [Test]
    public void AskingForNoCapabilityAtAllIsAnsweredWithNo()
    {
        //
        // Without this, a variable which happens to hold NONE would report every model as able to
        // do it, because every set contains the empty set.
        //
        var profile = new ModelProfile { Capabilities = Capability.TEXT_INPUT };

        Assert.That(profile.Has(Capability.NONE), Is.False);
    }

    [Test]
    public void AChangeOnlyTouchesWhatItStates()
    {
        var before = new ModelProfile
        {
            Capabilities = Capability.TEXT_INPUT | Capability.WEB_SEARCH,
            Reasoning = ReasoningSupport.OPTIONAL,
            Context = ContextWindow.Of(128_000),
        };

        var after = new ModelProfileChange { Removes = Capability.WEB_SEARCH }.ApplyTo(before);

        Assert.Multiple(() =>
        {
            Assert.That(after.Capabilities, Is.EqualTo(Capability.TEXT_INPUT));
            Assert.That(after.Reasoning, Is.EqualTo(ReasoningSupport.OPTIONAL), "A change saying nothing about reasoning must not reset it.");
            Assert.That(after.Context, Is.EqualTo(before.Context), "A change saying nothing about the context window must not reset it.");
        });
    }

    [Test]
    public void WhatAChangeTakesAwayWinsOverWhatItAdds()
    {
        var change = new ModelProfileChange
        {
            Adds = Capability.TEXT_INPUT | Capability.WEB_SEARCH,
            Removes = Capability.WEB_SEARCH,
        };

        Assert.That(change.ApplyTo(ModelProfile.UNKNOWN).Capabilities, Is.EqualTo(Capability.TEXT_INPUT));
    }

    [Test]
    public void AProfileNeverCarriesTheReasoningVocabulary()
    {
        //
        // The three reasoning members can be combined into answers no model can give, which is why
        // a profile states reasoning in one field instead. A rule declaring one of them has made a
        // mistake; that it cannot reach the answer is the second line of defence, not the first.
        //
        var change = new ModelProfileChange
        {
            Adds = Capability.TEXT_INPUT | Capability.ALWAYS_REASONING,
            Reasoning = ReasoningSupport.ALWAYS,
        };

        var profile = change.ApplyTo(ModelProfile.UNKNOWN);

        Assert.Multiple(() =>
        {
            Assert.That(profile.Capabilities, Is.EqualTo(Capability.TEXT_INPUT));
            Assert.That(profile.Has(Capability.ALWAYS_REASONING), Is.False);
            Assert.That(profile.Reasoning, Is.EqualTo(ReasoningSupport.ALWAYS));
        });
    }
}