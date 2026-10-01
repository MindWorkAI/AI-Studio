using AIStudio.Provider.HuggingFace;

namespace AIStudio.Tests.Provider;

/// <summary>
/// Checks which window a model has when it is reached through the Hugging Face router.
/// </summary>
/// <remarks>
/// The router is the one provider where the window does not belong to the model: the same weights
/// run behind several inference providers, each configured by somebody else, and which of them
/// answers depends on what the user chose.
/// </remarks>
[TestFixture]
public sealed class HFModelTests
{
    private const string AUTOMATIC = "";

    private static readonly HFModel SERVED_BY_THREE = new("deepseek-ai/DeepSeek-R1",
    [
        new("novita", "live", 64_000),
        new("together", "live", 128_000),
        new("fireworks-ai", "live", 160_000),
    ]);

    [Test]
    public void AChosenProviderAnswersForItself()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SERVED_BY_THREE.ContextWindowTokens("novita"), Is.EqualTo(64_000));
            Assert.That(SERVED_BY_THREE.ContextWindowTokens("together"), Is.EqualTo(128_000));
        });
    }

    [Test]
    public void AChosenProviderIsFoundHoweverItIsSpelled()
    {
        Assert.That(SERVED_BY_THREE.ContextWindowTokens("Novita"), Is.EqualTo(64_000));
    }

    [Test]
    public void LettingTheRouterChooseMeansTheSmallestWindowOnOffer()
    {
        //
        // Nobody knows which provider the router will take. Promising the largest window would walk
        // a conversation into an error the user could not see coming; the smallest one only warns
        // them earlier than strictly necessary.
        //
        Assert.That(SERVED_BY_THREE.ContextWindowTokens(AUTOMATIC), Is.EqualTo(64_000));
    }

    [Test]
    public void AProviderWhichIsNotServingDoesNotDecideAnything()
    {
        var oneIsDown = new HFModel("deepseek-ai/DeepSeek-R1",
        [
            new("novita", "staging", 8_000),
            new("together", "live", 128_000),
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(oneIsDown.ContextWindowTokens(AUTOMATIC), Is.EqualTo(128_000), "The small window belongs to a provider nobody can reach.");
            Assert.That(oneIsDown.ContextWindowTokens("novita"), Is.Null, "And asking for that provider by name does not bring it back either.");
        });
    }

    [Test]
    public void AProviderWhichDoesNotServeTheModelSaysNothingAboutIt()
    {
        Assert.That(SERVED_BY_THREE.ContextWindowTokens("cerebras"), Is.Null);
    }

    [Test]
    public void AWindowNobodyStatedIsSkippedRatherThanCountedAsNothing()
    {
        var halfStated = new HFModel("deepseek-ai/DeepSeek-R1",
        [
            new("novita", "live", null),
            new("together", "live", 128_000),
        ]);

        Assert.That(halfStated.ContextWindowTokens(AUTOMATIC), Is.EqualTo(128_000), "A missing number is not the smallest number.");
    }

    [Test]
    public void AModelNobodyServesHasNoWindow()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new HFModel("org/model", null).ContextWindowTokens(AUTOMATIC), Is.Null);
            Assert.That(new HFModel("org/model", []).ContextWindowTokens(AUTOMATIC), Is.Null);
        });
    }
}