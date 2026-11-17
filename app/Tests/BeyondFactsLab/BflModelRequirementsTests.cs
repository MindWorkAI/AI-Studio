using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Models;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks which models the BeyondFacts Lab accepts for its chats.
/// </summary>
[TestFixture]
public sealed class BflModelRequirementsTests
{
    [Test]
    public void AModelWithoutAStatementAboutItsWindowIsAccepted()
    {
        Assert.That(BflModelRequirements.HasEnoughContext(ContextWindow.UNKNOWN), Is.True);
    }

    [TestCase(8_192, false)]
    [TestCase(BflModelRequirements.MINIMUM_CONTEXT_TOKENS - 1, false)]
    [TestCase(BflModelRequirements.MINIMUM_CONTEXT_TOKENS, true)]
    [TestCase(128_000, true)]
    public void AKnownWindowNeedsTheMinimum(int defaultTokens, bool expected)
    {
        Assert.That(BflModelRequirements.HasEnoughContext(ContextWindow.Of(defaultTokens)), Is.EqualTo(expected));
    }

    [Test]
    public void TheOnboardingRunsWithASmallWindow()
    {
        var smallWindow = ContextWindow.Of(8_192);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(BflModelRequirements.HasEnoughContext(BflOnboardingThreadLocation.INSTANCE, smallWindow), Is.True, "The onboarding leaves out the guide.");
            Assert.That(BflModelRequirements.HasEnoughContext(new BflStepThreadLocation(Guid.NewGuid(), BflStep.FRAMEWORK), smallWindow), Is.False);
            Assert.That(BflModelRequirements.HasEnoughContext(new BflStepThreadLocation(Guid.NewGuid(), BflStep.FRAMEWORK), ContextWindow.Of(BflModelRequirements.MINIMUM_CONTEXT_TOKENS)), Is.True);
        }
    }

    [Test]
    public void AWindowWhichOnlyAnOperatorCouldRaiseIsNotEnough()
    {
        Assert.That(BflModelRequirements.HasEnoughContext(ContextWindow.Of(16_384, 131_072)), Is.False, "What a model reads as it ships counts, not what it might read.");
    }
}