using AIStudio.Models.Registry;
using AIStudio.Provider;
using AIStudio.Tests.Models.Corpus;

namespace AIStudio.Tests.Models;

/// <summary>
/// Holds the rules to what a model is made for.
/// </summary>
/// <remarks>
/// What a model can do and what it is for are two questions, and until now two pieces of code
/// answered them, each walking the same name with rules of its own. This is the test which says the
/// second answer did not change when it moved: the marker list is still there and still answers, so
/// every example can be put to both and the two have to agree.
///
/// The day the call sites move to the profile, the marker list goes and the test below which asks
/// it goes with it. What stays is the first test: the examples say what each name is, in words a
/// person can check against a model card.
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
    public void TheMarkersBeingReplacedAnswerEveryExampleTheSameWay()
    {
        Assert.Multiple(() =>
        {
            foreach (var example in ModelKindCorpus.ENTRIES)
            {
                var today = new Model(example.ModelId, null).DetermineKind();
                var wanted = example.AnsweredTodayAs ?? example.Kind;
                var because = example.AnsweredTodayAs is null
                    ? $"{example.Provider} \"{example.ModelId}\" is sorted differently by the rules than by the markers they replace."
                    : $"{example.Provider} \"{example.ModelId}\": {example.Reason}";

                Assert.That(today, Is.EqualTo(wanted), because);
            }
        });
    }

    [Test]
    public void EveryModelOfTheCapabilityCorpusKeepsTheKindItHasToday()
    {
        //
        // The examples above are names chosen to reach a rule. This asks the other way round: the
        // corpus is full of models nobody wants sorted anywhere but into a chat, and a word inside
        // one of those names claiming a kind would take the model out of the user's list without
        // anything else going wrong.
        //
        Assert.Multiple(() =>
        {
            foreach (var entry in ModelCorpus.ENTRIES)
            {
                var today = new Model(entry.ModelId, null).DetermineKind();
                var rebuilt = ModelRegistry.Shared.Profile(entry.Provider, entry.ModelId).Kind;
                var wanted = ModelKindCorpus.AnsweredTodayAs(entry.Provider, entry.ModelId) ?? rebuilt;

                Assert.That(today, Is.EqualTo(wanted), $"{entry.Provider} \"{entry.ModelId}\" is sorted as {rebuilt} by the rules and as {today} by the markers they replace.");
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