using System.Text.Json;

using AIStudio.Provider;
using AIStudio.Settings;

using SelfHostedModelsResponse = AIStudio.Provider.SelfHosted.ModelsResponse;

namespace AIStudio.Tests.Provider;

/// <summary>
/// Checks how the models of somebody's own server are sorted into the three lists they are offered in.
/// </summary>
/// <remarks>
/// The engines answer one route with everything they serve, and that answer says nothing about what
/// any of it is made for: an ID, the word "model", and at Ollama a timestamp. Which list a model
/// ends up in is therefore decided afterwards, and for a long time it was decided by looking for
/// the word "embed" in the name -- the chat list was everything without it, the embedding list
/// everything with it, and the transcription list was not filtered at all.
///
/// The body below is the real answer of a local Ollama, copied off the route rather than written
/// from memory, and it holds the two names which that reading got wrong. What it costs is visible
/// in the assertions: an embedding model in the chat list is one somebody picks and then waits for
/// an answer which never comes.
/// </remarks>
[TestFixture]
public sealed class SelfHostedModelListTests
{
    private static readonly JsonSerializerOptions AS_THE_PROVIDERS_READ_IT = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// What "GET /v1/models" answers on a local Ollama, shortened to the fields it sends.
    /// </summary>
    private const string WHAT_A_LOCAL_OLLAMA_ANSWERS =
        """
        {
            "object": "list",
            "data": [
                { "id": "all-minilm:latest", "object": "model", "created": 1789809739, "owned_by": "library" },
                { "id": "bge-m3:latest", "object": "model", "created": 1789809702, "owned_by": "library" },
                { "id": "qwen3-embedding:0.6b", "object": "model", "created": 1788699716, "owned_by": "library" },
                { "id": "qwen3-embedding:4b", "object": "model", "created": 1788699586, "owned_by": "library" },
                { "id": "qwen3-embedding:latest", "object": "model", "created": 1788699253, "owned_by": "library" },
                { "id": "qwen3.8:latest", "object": "model", "created": 1788698492, "owned_by": "library" },
                { "id": "gpt-oss:latest", "object": "model", "created": 1756645805, "owned_by": "library" }
            ]
        }
        """;

    private static IReadOnlyList<Model> TheModelsTheEngineListed()
    {
        var response = JsonSerializer.Deserialize<SelfHostedModelsResponse>(WHAT_A_LOCAL_OLLAMA_ANSWERS, AS_THE_PROVIDERS_READ_IT);
        Assert.That(response.Data, Is.Not.Null, "The answer has to be readable before anything can be sorted out of it.");

        return response.Data!
            .Where(model => !string.IsNullOrWhiteSpace(model.Id))
            .Select(model => new Model(model.Id, null))
            .ToList();
    }

    [Test]
    public void TheChatListHoldsWhatSomebodyCanTalkTo()
    {
        var chatModels = TheModelsTheEngineListed()
            .Where(model => model.IsChatModel(LLMProviders.SELF_HOSTED))
            .Select(model => model.Id)
            .ToList();

        Assert.That(chatModels, Is.EquivalentTo(new[] { "qwen3.8:latest", "gpt-oss:latest" }));
    }

    [Test]
    public void TheEmbeddingListHoldsTheModelsWhichSayNothingAboutEmbedding()
    {
        var embeddingModels = TheModelsTheEngineListed()
            .Where(model => model.IsEmbeddingModel(LLMProviders.SELF_HOSTED))
            .Select(model => model.Id)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(embeddingModels, Does.Contain("bge-m3:latest"), "Named after the family which built it, with no word about what it does.");
            Assert.That(embeddingModels, Does.Contain("all-minilm:latest"), "The same, and without the organization which used to be the only marker.");
            Assert.That(embeddingModels, Has.Count.EqualTo(5), "The three Qwen embedding tags belong here as well, and nothing else does.");
        });
    }

    [Test]
    public void NothingIsInTwoListsAtOnce()
    {
        //
        // The two lists were cut from one name with one word, so a model could only ever be in one
        // of them. They are cut by two questions now, and two questions can both say yes.
        //
        var models = TheModelsTheEngineListed();
        var inBothLists = models
            .Where(model => model.IsChatModel(LLMProviders.SELF_HOSTED) && model.IsEmbeddingModel(LLMProviders.SELF_HOSTED))
            .Select(model => model.Id)
            .ToList();

        Assert.That(inBothLists, Is.Empty);
    }

    [Test]
    public void AnEngineWithoutASpeechModelOffersNoneForTranscription()
    {
        //
        // Ollama serves no speech-to-text model of its own, and the list said otherwise: it was
        // handed through unfiltered, so all seven of these stood there to be picked.
        //
        var transcriptionModels = TheModelsTheEngineListed()
            .Where(model => model.IsTranscriptionModel(LLMProviders.SELF_HOSTED))
            .ToList();

        Assert.That(transcriptionModels, Is.Empty);
    }

    [Test]
    public void ASpeechModelOnSuchAServerIsOfferedForTranscription()
    {
        //
        // The other half of the one above: the empty list has to come from there being no speech
        // model, not from the question never saying yes on this provider.
        //
        var models = new[] { "whisper-large-v3", "faster-whisper-large-v3", "canary-1b-flash" }
            .Select(id => new Model(id, null))
            .Where(model => model.IsTranscriptionModel(LLMProviders.SELF_HOSTED))
            .Select(model => model.Id)
            .ToList();

        Assert.That(models, Has.Count.EqualTo(3));
    }
}