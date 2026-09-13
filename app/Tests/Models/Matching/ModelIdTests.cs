using AIStudio.Models.Matching;

namespace AIStudio.Tests.Models.Matching;

/// <summary>
/// Checks that every provider's way of writing a name arrives in the one form the rules are in.
/// </summary>
/// <remarks>
/// The spellings below are not invented. They are the ones the old rules had to spell out over and
/// over, and the ones its comments quote: an Ollama tag, a Fireworks path, a hub prefix, and the
/// whole sentence Blablador answers with.
/// </remarks>
[TestFixture]
public sealed class ModelIdTests
{
    [TestCase("gpt-5.1", "gpt-5.1")]
    [TestCase("GPT-5.1", "gpt-5.1")]
    [TestCase("qwen3.8:27b-mlx", "qwen3.8-27b-mlx")]
    [TestCase("accounts/fireworks/models/llama-v3p1-405b-instruct", "accounts-fireworks-models-llama-v3p1-405b-instruct")]
    [TestCase("meta-llama/Llama-3.3-70B-Instruct", "meta-llama-llama-3.3-70b-instruct")]
    [TestCase("10 - Muse Glimmer 30b - the newest META model", "10-muse-glimmer-30b-the-newest-meta-model")]
    [TestCase("anthropic.claude-3-5-sonnet-20241022-v2:0", "anthropic.claude-3-5-sonnet-20241022-v2-0")]
    public void ANameArrivesInTheFormTheRulesAreWrittenIn(string reported, string expected) => Assert.That(new ModelId(reported).Normalized, Is.EqualTo(expected));

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("---")]
    [TestCase(" / : - ")]
    public void ANameWhichIsNothingButSeparatorsIsEmpty(string reported)
    {
        var id = new ModelId(reported);

        Assert.Multiple(() =>
        {
            Assert.That(id.IsEmpty, Is.True);
            Assert.That(id.Normalized, Is.Empty);
        });
    }

    [Test]
    public void ANameKeepsTheSpellingAPersonSees()
    {
        var id = new ModelId("Qwen3.8:27B-MLX");

        Assert.Multiple(() =>
        {
            Assert.That(id.Original, Is.EqualTo("Qwen3.8:27B-MLX"));
            Assert.That(id.ToString(), Is.EqualTo("Qwen3.8:27B-MLX"));
        });
    }

    [Test]
    public void ADefaultModelIdIsEmptyRatherThanBroken()
    {
        ModelId untouched = default;

        Assert.Multiple(() =>
        {
            Assert.That(untouched.IsEmpty, Is.True);
            Assert.That(untouched.Original, Is.Empty);
            Assert.That(untouched.Normalized, Is.Empty);
            Assert.That(untouched.Segments.GetEnumerator().MoveNext(), Is.False);
        });
    }

    [Test]
    public void TwoNamesWrittenDifferentlyAreTheSameName()
    {
        var fromOllama = new ModelId("Qwen3.8:27b");
        var fromHub = new ModelId("qwen3.8-27b");

        Assert.Multiple(() =>
        {
            Assert.That(fromOllama, Is.EqualTo(fromHub));
            Assert.That(fromOllama.GetHashCode(), Is.EqualTo(fromHub.GetHashCode()));
        });
    }

    [Test]
    public void ANameIsWalkedOneNamePartAtATime()
    {
        var parts = new List<string>();
        foreach (var part in new ModelId("deepseek-r1-distill-llama-70b").Segments)
            parts.Add(part.ToString());

        Assert.That(parts, Is.EqualTo(new[] { "deepseek", "r1", "distill", "llama", "70b" }));
    }

    [Test]
    public void AVersionDotDoesNotStartANewNamePart()
    {
        //
        // llama3 and llama3.1 are different models and only the latter calls functions, so the dot
        // has to stay inside the part rather than cut it in two.
        //
        var parts = new List<string>();
        foreach (var part in new ModelId("qwen3.8:27b").Segments)
            parts.Add(part.ToString());

        Assert.That(parts, Is.EqualTo(new[] { "qwen3.8", "27b" }));
    }

    [TestCase("gpt-5-chat-latest", "gpt-5", true)]
    [TestCase("gpt-55-turbo", "gpt-5", false)]
    [TestCase("gpt-5.1", "gpt-5", false)]
    [TestCase("gpt-5", "gpt-5", true)]
    [TestCase("gpt-5.1-codex", "gpt-5.1", true)]
    public void ANameBeginsWithATextOnlyWhenANamePartEndsThere(string name, string text, bool expected) => Assert.That(new ModelId(name).StartsWithSegments(text), Is.EqualTo(expected));

    [TestCase("deepseek-r1-distill-llama-70b", "llama", true)]
    [TestCase("deepseek-r1-distill-llama-70b", "deepseek-r1", true)]
    [TestCase("meta-llama-llama-3.3-70b-instruct", "llama", true)]
    [TestCase("yi-34b-chat", "yi", true)]
    [TestCase("granite-embedding-278m", "yi", false)]
    [TestCase("qwen3.8-27b", "qwen3", false)]
    [TestCase("nvidia-nemotron-3.5-lightning-30b-a3b-nvfp4", "v", false)]
    public void ATextIsFoundInANameOnlyBetweenTwoNamePartBoundaries(string name, string text, bool expected) => Assert.That(new ModelId(name).ContainsSegments(text), Is.EqualTo(expected));

    [Test]
    public void ATextIsFoundAtALaterBoundaryWhenTheFirstOccurrenceSitsInsideANamePart()
    {
        //
        // The first "llama" here sits inside "meta-llama"; the rule still has to find the one which
        // stands on its own.
        //
        Assert.That(new ModelId("metallama/llama-3.3-70b").ContainsSegments("llama"), Is.True);
    }

    [Test]
    public void ATextIsFoundAnywhereWhenTheRuleAsksForThat() => Assert.That(new ModelId("qwen3.8-27b").ContainsText("3.8"), Is.True);
}