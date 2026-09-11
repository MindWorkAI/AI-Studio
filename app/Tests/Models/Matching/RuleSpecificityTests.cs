using AIStudio.Models;
using AIStudio.Models.Matching;
using AIStudio.Provider;

namespace AIStudio.Tests.Models.Matching;

/// <summary>
/// Checks the order in which the criteria are weighed against each other.
/// </summary>
/// <remarks>
/// Each test below changes exactly one criterion and leaves the others equal, which is the only way
/// to state what beats what. The order itself is the decision this whole rebuild rests on, so it is
/// written down here rather than left to be inferred from how the rules happen to behave.
/// </remarks>
[TestFixture]
public sealed class RuleSpecificityTests
{
    [Test]
    public void NamingTheWholeModelBeatsNamingHowItsNameBegins() => AssertMoreSpecific(
        new() { Kind = MatchKind.EXACT, Text = "gpt-5" },
        new() { Kind = MatchKind.PREFIX, Text = "gpt-5" });

    [Test]
    public void NamingHowANameBeginsBeatsNamingAPartOfIt() => AssertMoreSpecific(
        new() { Kind = MatchKind.PREFIX, Text = "gpt-5" },
        new() { Kind = MatchKind.SEGMENT, Text = "gpt-5" });

    [Test]
    public void NamingAWholeNamePartBeatsAppearingSomewhereInside() => AssertMoreSpecific(
        new() { Kind = MatchKind.SEGMENT, Text = "gpt-5" },
        new() { Kind = MatchKind.SUBSTRING, Text = "gpt-5" });

    [Test]
    public void SpellingOutMoreOfTheNameBeatsSpellingOutLess() => AssertMoreSpecific(
        new() { Kind = MatchKind.SEGMENT, Text = "deepseek-r1" },
        new() { Kind = MatchKind.SEGMENT, Text = "llama" });

    [Test]
    public void RequiringAFurtherNamePartBeatsNotRequiringOne() => AssertMoreSpecific(
        new() { Kind = MatchKind.SEGMENT, Text = "llama", AlsoContains = ["vision"] },
        new() { Kind = MatchKind.SEGMENT, Text = "llama" });

    [Test]
    public void BeingWrittenForOneProviderBeatsHoldingEverywhere() => AssertMoreSpecific(
        new() { Kind = MatchKind.SEGMENT, Text = "qwq", OnlyOn = LLMProviders.ALIBABA_CLOUD },
        new() { Kind = MatchKind.SEGMENT, Text = "qwq" });

    [Test]
    public void BeingWrittenForBothAProviderAndAVendorBeatsEitherAlone() => AssertMoreSpecific(
        new() { Kind = MatchKind.SEGMENT, Text = "qwq", OnlyOn = LLMProviders.ALIBABA_CLOUD, OnlyFrom = ModelVendor.ALIBABA },
        new() { Kind = MatchKind.SEGMENT, Text = "qwq", OnlyOn = LLMProviders.ALIBABA_CLOUD });

    [Test]
    public void AHandWrittenRankOverrulesEverythingTheComputationWouldSay()
    {
        //
        // The emergency exit has to leave the building. A rank which the length of some other
        // pattern can overrule would not rescue the case it was written for, so it is weighed
        // before every computed criterion rather than after them.
        //
        AssertMoreSpecific(
            new() { Kind = MatchKind.SUBSTRING, Text = "r1", ExplicitRank = 1 },
            new() { Kind = MatchKind.EXACT, Text = "deepseek-r1-distill-llama-70b" });
    }

    [Test]
    public void ANegativeRankPushesARuleBehindEverythingElse() => AssertMoreSpecific(
        new() { Kind = MatchKind.SUBSTRING, Text = "r1" },
        new() { Kind = MatchKind.EXACT, Text = "deepseek-r1", ExplicitRank = -1 });

    [Test]
    public void TwoRulesSayingTheSameAmountAreEqual()
    {
        var one = RuleSpecificity.Of(new() { Kind = MatchKind.SEGMENT, Text = "llama" });
        var other = RuleSpecificity.Of(new() { Kind = MatchKind.SEGMENT, Text = "qwen3" });

        Assert.That(one.CompareTo(other), Is.Zero);
    }

    private static void AssertMoreSpecific(MatchPattern expectedWinner, MatchPattern expectedLoser)
    {
        var winner = RuleSpecificity.Of(expectedWinner);
        var loser = RuleSpecificity.Of(expectedLoser);

        Assert.Multiple(() =>
        {
            Assert.That(winner.CompareTo(loser), Is.GreaterThan(0));
            Assert.That(loser.CompareTo(winner), Is.LessThan(0), "The comparison has to say the same thing in both directions.");
        });
    }
}