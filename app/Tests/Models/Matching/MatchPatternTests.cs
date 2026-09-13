using AIStudio.Models;
using AIStudio.Models.Matching;
using AIStudio.Provider;

namespace AIStudio.Tests.Models.Matching;

/// <summary>
/// Checks what a single pattern claims, before anything compares two of them.
/// </summary>
[TestFixture]
public sealed class MatchPatternTests
{
    [Test]
    public void APatternBoundToAProviderStaysSilentEverywhereElse()
    {
        //
        // On Alibaba, "qwq" is qwq-plus, a commercial model. Everywhere else it is the open weights
        // built on Qwen 2.5. Two different models, one name, and the binding is what tells them
        // apart without anybody writing an order.
        //
        var onAlibaba = new MatchPattern { Kind = MatchKind.SEGMENT, Text = "qwq", OnlyOn = LLMProviders.ALIBABA_CLOUD };
        var name = new ModelId("qwq-32b");

        Assert.Multiple(() =>
        {
            Assert.That(onAlibaba.Matches(name, LLMProviders.ALIBABA_CLOUD, ModelVendor.UNKNOWN), Is.True);
            Assert.That(onAlibaba.Matches(name, LLMProviders.SELF_HOSTED, ModelVendor.UNKNOWN), Is.False);
        });
    }

    [Test]
    public void APatternBoundToAVendorStaysSilentWhenSomebodyElseBuiltTheModel()
    {
        var fromAnthropic = new MatchPattern { Kind = MatchKind.SEGMENT, Text = "claude", OnlyFrom = ModelVendor.ANTHROPIC };
        var name = new ModelId("claude-sonnet-4-0");

        Assert.Multiple(() =>
        {
            Assert.That(fromAnthropic.Matches(name, LLMProviders.LITE_LLM, ModelVendor.ANTHROPIC), Is.True);
            Assert.That(fromAnthropic.Matches(name, LLMProviders.LITE_LLM, ModelVendor.UNKNOWN), Is.False);
        });
    }

    [Test]
    public void AnExtraConditionHasToBeAWholeNamePartToo()
    {
        var withVision = new MatchPattern { Kind = MatchKind.SEGMENT, Text = "qwen3.8", AlsoContains = ["vl"] };

        Assert.Multiple(() =>
        {
            Assert.That(withVision.Matches(new ModelId("qwen3.8-27b-vl"), LLMProviders.SELF_HOSTED, ModelVendor.UNKNOWN), Is.True);
            Assert.That(withVision.Matches(new ModelId("qwen3.8-27b"), LLMProviders.SELF_HOSTED, ModelVendor.UNKNOWN), Is.False);
            Assert.That(withVision.Matches(new ModelId("qwen3.8-27b-vllm"), LLMProviders.SELF_HOSTED, ModelVendor.UNKNOWN), Is.False, "\"vl\" inside another name part is not the vision variant.");
        });
    }

    [Test]
    public void AForbiddenNamePartRulesAPatternOut()
    {
        //
        // Salamandra does not call functions, except for the variant which was built for it.
        //
        var withoutTools = new MatchPattern { Kind = MatchKind.SEGMENT, Text = "salamandra", NotContains = ["tools"] };

        Assert.Multiple(() =>
        {
            Assert.That(withoutTools.Matches(new ModelId("salamandra-7b-instruct"), LLMProviders.SELF_HOSTED, ModelVendor.UNKNOWN), Is.True);
            Assert.That(withoutTools.Matches(new ModelId("salamandra-7b-instruct-tools"), LLMProviders.SELF_HOSTED, ModelVendor.UNKNOWN), Is.False);
        });
    }

    [TestCase("gpt-5.1", true)]
    [TestCase("qwen3.8-27b", true)]
    [TestCase("GPT-5.1", false)]
    [TestCase("gpt_5", false)]
    [TestCase("gpt 5", false)]
    [TestCase("-gpt-5", false)]
    [TestCase("gpt--5", false)]
    [TestCase("", false)]
    public void APatternHasToBeWrittenTheWayANameArrives(string text, bool expected) => Assert.That(MatchPattern.IsNormalized(text), Is.EqualTo(expected));

    [Test]
    public void APatternWhichCannotMatchAnythingSaysSo()
    {
        var malformed = new MatchPattern { Kind = MatchKind.SEGMENT, Text = "gpt-5", AlsoContains = ["Codex"] };

        Assert.That(malformed.IsWellFormed, Is.False);
    }

    [Test]
    public void TwoPatternsSayingTheSameThingInADifferentOrderHaveTheSameSignature()
    {
        var one = new MatchPattern { Kind = MatchKind.SEGMENT, Text = "llama", AlsoContains = ["instruct", "70b"] };
        var other = new MatchPattern { Kind = MatchKind.SEGMENT, Text = "llama", AlsoContains = ["70b", "instruct"] };

        Assert.That(one.Signature(), Is.EqualTo(other.Signature()));
    }

    [TestCase(MatchKind.EXACT, "deepseek-r1", "deepseek")]
    [TestCase(MatchKind.PREFIX, "gpt-5.1", "gpt")]
    [TestCase(MatchKind.SEGMENT, "qwen3.8", "qwen3.8")]
    [TestCase(MatchKind.SUBSTRING, "3.8", "")]
    public void ThePatternTellsTheIndexWhichNamePartToFileItUnder(MatchKind kind, string text, string expected)
    {
        var pattern = new MatchPattern { Kind = kind, Text = text };

        Assert.That(pattern.IndexKey().ToString(), Is.EqualTo(expected));
    }
}