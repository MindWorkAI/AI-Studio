using AIStudio.Models;
using AIStudio.Models.Live;
using AIStudio.Provider;

namespace AIStudio.Tests.Models.Live;

/// <summary>
/// Checks what a running installation is allowed to say about the models it serves.
/// </summary>
/// <remarks>
/// Every test here builds its own store rather than using the shared one. What a provider reported
/// is state which outlives a single question, and a test leaving some of it behind would decide
/// what the next test sees.
/// </remarks>
[TestFixture]
public sealed class ListedModelsTests
{
    private const string ONE_MACHINE = "11111111-1111-1111-1111-111111111111";
    private const string ANOTHER_MACHINE = "22222222-2222-2222-2222-222222222222";
    private const string MODEL = "qwen3-32b";

    /// <summary>
    /// A model the rules have something to say about, so that a report has something to contradict.
    /// </summary>
    private static readonly ModelProfile WHAT_THE_RULES_SAY = new()
    {
        Capabilities = Capability.TEXT_INPUT | Capability.TEXT_OUTPUT | Capability.MULTIPLE_IMAGE_INPUT,
        Kind = ModelKind.CHAT,
        Context = ContextWindow.Of(131_072, 262_144),
        Images = new(null, 20),
    };

    [Test]
    public void AMachineWhichWasNeverAskedSaysNothing()
    {
        var listed = new ListedModels();

        Assert.Multiple(() =>
        {
            Assert.That(listed.Of(ONE_MACHINE, MODEL), Is.EqualTo(ModelListing.NOTHING));
            Assert.That(listed.Of(ONE_MACHINE, MODEL).IsKnown, Is.False);
        });
    }

    [Test]
    public void WhatOneMachineSaysIsNotWhatAnotherSays()
    {
        //
        // The same weights behind two engines, each started by somebody who decided for themselves.
        // This is the whole reason these numbers are kept per configured instance.
        //
        var listed = new ListedModels();
        listed.Report(ONE_MACHINE, [new(MODEL, ContextWindow.Of(32_768))]);
        listed.Report(ANOTHER_MACHINE, [new(MODEL, ContextWindow.Of(8_192))]);

        Assert.Multiple(() =>
        {
            Assert.That(listed.Of(ONE_MACHINE, MODEL).Context.DefaultTokens, Is.EqualTo(32_768));
            Assert.That(listed.Of(ANOTHER_MACHINE, MODEL).Context.DefaultTokens, Is.EqualTo(8_192));
        });
    }

    [Test]
    public void WhatAMachineNoLongerServesStopsAnswering()
    {
        var listed = new ListedModels();
        listed.Report(ONE_MACHINE, [new(MODEL, ContextWindow.Of(32_768)), new("gemma3-27b", ContextWindow.Of(16_384))]);
        listed.Report(ONE_MACHINE, [new("gemma3-27b", ContextWindow.Of(16_384))]);

        Assert.Multiple(() =>
        {
            Assert.That(listed.Of(ONE_MACHINE, MODEL), Is.EqualTo(ModelListing.NOTHING), "The engine was restarted without it, so nothing is known about it any more.");
            Assert.That(listed.Of(ONE_MACHINE, "gemma3-27b").Context.DefaultTokens, Is.EqualTo(16_384));
        });
    }

    [Test]
    public void AMachineWhichHalvedItsWindowIsBelievedTheSecondTimeToo()
    {
        var listed = new ListedModels();
        listed.Report(ONE_MACHINE, [new(MODEL, ContextWindow.Of(32_768))]);
        listed.Report(ONE_MACHINE, [new(MODEL, ContextWindow.Of(16_384))]);

        Assert.That(listed.Of(ONE_MACHINE, MODEL).Context.DefaultTokens, Is.EqualTo(16_384));
    }

    [TestCase("Qwen3-32B")]
    [TestCase("qwen3-32b")]
    public void AModelSomebodyTypedIsStillTheSameModel(string asConfigured)
    {
        //
        // An organization writes the model of a provider into its configuration plugin by hand,
        // and the availability check already treats such a name as the same model whatever case it
        // was typed in. Being stricter here would leave exactly those people without the numbers.
        //
        var listed = new ListedModels();
        listed.Report(ONE_MACHINE, [new("qwen3-32b", ContextWindow.Of(32_768))]);

        Assert.That(listed.Of(ONE_MACHINE, asConfigured).Context.DefaultTokens, Is.EqualTo(32_768));
    }

    [Test]
    public void AnInstanceWithoutAnIdIsNothingToRemember()
    {
        var listed = new ListedModels();
        listed.Report(string.Empty, [new(MODEL, ContextWindow.Of(32_768))]);

        Assert.Multiple(() =>
        {
            Assert.That(listed.Of(string.Empty, MODEL), Is.EqualTo(ModelListing.NOTHING));
            Assert.That(listed.Of(ONE_MACHINE, MODEL), Is.EqualTo(ModelListing.NOTHING), "One nameless report does not become every machine's answer.");
        });
    }

    [Test]
    public void AModelTheMachineSaidNothingAboutIsNotStored()
    {
        var listed = new ListedModels();
        listed.Report(ONE_MACHINE, [new(MODEL, ContextWindow.UNKNOWN), new(string.Empty, ContextWindow.Of(32_768))]);

        Assert.That(listed.Of(ONE_MACHINE, MODEL), Is.EqualTo(ModelListing.NOTHING));
    }

    [Test]
    public void AReportedWindowReplacesTheWholeWindow()
    {
        var after = new ModelListing(MODEL, ContextWindow.Of(32_768)).ApplyTo(WHAT_THE_RULES_SAY);

        Assert.Multiple(() =>
        {
            Assert.That(after.Context.DefaultTokens, Is.EqualTo(32_768));
            Assert.That(after.Context.RaisableToTokens, Is.Null, "What the weights could be raised to is not a number anybody reaches without restarting this engine.");
        });
    }

    [Test]
    public void AWindowSaysNothingAboutAnythingElse()
    {
        var after = new ModelListing(MODEL, ContextWindow.Of(32_768)).ApplyTo(WHAT_THE_RULES_SAY);

        Assert.Multiple(() =>
        {
            Assert.That(after.Capabilities, Is.EqualTo(WHAT_THE_RULES_SAY.Capabilities));
            Assert.That(after.Images, Is.EqualTo(WHAT_THE_RULES_SAY.Images));
            Assert.That(after.Kind, Is.EqualTo(WHAT_THE_RULES_SAY.Kind));
        });
    }

    [Test]
    public void SayingNothingKeepsEverythingTheRulesWorkedOut()
    {
        Assert.That(ModelListing.NOTHING.ApplyTo(WHAT_THE_RULES_SAY), Is.EqualTo(WHAT_THE_RULES_SAY));
    }

    [Test]
    public void AWindowAProviderStatedIsTakenAsItIs()
    {
        Assert.That(ModelListing.For(MODEL, 32_768).Context.DefaultTokens, Is.EqualTo(32_768));
    }

    [TestCase(0, TestName = "A window of no tokens")]
    [TestCase(-1, TestName = "A window of negative tokens")]
    [TestCase(null, TestName = "No window at all")]
    public void AWindowWhichIsNoWidthIsDroppedRatherThanRepaired(int? tokens)
    {
        //
        // Every dialect comes through this one factory, so a provider answering with something
        // nobody can interpret falls back to what the rules say -- and does so the same way for
        // all of them, rather than once per provider and slightly differently each time.
        //
        Assert.That(ModelListing.For(MODEL, tokens), Is.EqualTo(ModelListing.NOTHING));
    }

    [Test]
    public void AnEntryWithoutANameIsNoListing()
    {
        Assert.That(ModelListing.For(string.Empty, 32_768), Is.EqualTo(ModelListing.NOTHING));
    }
}