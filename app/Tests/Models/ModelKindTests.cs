using AIStudio.Models.Registry;
using AIStudio.Provider;
using AIStudio.Tests.Models.Corpus;

namespace AIStudio.Tests.Models;

/// <summary>
/// Holds the rules to what a model is made for.
/// </summary>
/// <remarks>
/// What a model can do and what it is for are two questions, and they used to be answered by two
/// pieces of code, each walking the same name with rules of its own. While both existed, the tests
/// here held one against the other. The marker list is gone now, and with it the comparison: the
/// corpus-wide check moved into the snapshot, which carries the kind of every model in a column of
/// its own.
///
/// What is left says what a name is, in words a person can check against a model card -- and holds
/// the handful of decisions where the rules deliberately answer something else than the markers did.
/// Those stand in the corpus next to the name, with the reason.
/// </remarks>
[TestFixture]
public sealed class ModelKindTests
{
    [Test]
    public void EveryExampleIsRecognizedAsWhatItIsMadeFor()
    {
        Assert.Multiple(() =>
        {
            foreach (var example in ModelKindCorpus.ENTRIES)
            {
                var profile = ModelRegistry.Shared.Profile(example.Provider, example.ModelId);

                Assert.That(profile.Kind, Is.EqualTo(example.Kind), $"{example.Provider} \"{example.ModelId}\"");
            }
        });
    }

    [Test]
    public void AModelWhichIsNoKindOfItsOwnIsAChatModel()
    {
        //
        // The fallback, and the direction it points in. A model we fail to recognize stays visible
        // to the user rather than disappearing, because a provider adding a family we have never
        // seen is the normal case and a user paying for it is the one who would notice.
        //
        var profile = ModelRegistry.Shared.Profile(LLMProviders.SELF_HOSTED, "a-model-nobody-has-heard-of");

        Assert.That(profile.Kind, Is.EqualTo(ModelKind.CHAT));
    }

    [Test]
    public void AModelKeepsWhatItsFamilySaysWhenAnotherWordSaysWhatItIsFor()
    {
        //
        // The reason these are modifiers. Llama-Guard is a Llama, and everything the Llama rules
        // state about it stays true; it is simply not something to chat with. Written as a selector,
        // "guard" would have to beat "llama" -- two substrings of the same length, which is a tie,
        // which is an error rather than an answer.
        //
        var profile = ModelRegistry.Shared.Profile(LLMProviders.SELF_HOSTED, "llama-guard-3-8b");

        Assert.Multiple(() =>
        {
            Assert.That(profile.Kind, Is.EqualTo(ModelKind.MODERATION));
            Assert.That(profile.Has(Capability.TEXT_INPUT), Is.True, "The family which chose the model still speaks for it.");
        });
    }

    [Test]
    public void ARerankerIsARerankerAndNotTheEmbeddingModelItIsNamedAfter()
    {
        var resolution = ModelRegistry.Shared.Explain(LLMProviders.SELF_HOSTED, "bge-reranker-v2-m3");

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Profile.Kind, Is.EqualTo(ModelKind.RERANKING));
            Assert.That(resolution.Modifiers.Select(modifier => modifier.Pattern.Text), Does.Contain("bge"), "Both words match; the ranked one has to be the one which gets the last word.");
        });
    }
}