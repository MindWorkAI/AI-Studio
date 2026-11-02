using System.Text.Json;

using AIStudio.Provider;
using AIStudio.Provider.Anthropic;
using AIStudio.Provider.Google;
using AIStudio.Provider.Groq;
using AIStudio.Provider.HuggingFace;
using AIStudio.Provider.OpenAI;
using AIStudio.Provider.OpenRouter;
using AIStudio.Provider.Requesty;

using AnthropicResponseStreamLine = AIStudio.Provider.Anthropic.ResponseStreamLine;
using FireworksResponseStreamLine = AIStudio.Provider.Fireworks.ResponseStreamLine;
using PerplexityResponseStreamLine = AIStudio.Provider.Perplexity.ResponseStreamLine;

namespace AIStudio.Tests.Serialization;

public static partial class JsonTestRoots
{
    /// <summary>
    /// What AI Studio sends to LLM providers.
    /// </summary>
    public static IEnumerable<TestCaseData> ProviderRequests()
    {
        TestHost.PrepareStaticState();
        var options = ProviderJsonOptions.OPTIONS;

        yield return CaseWithNamespace(typeof(ChatCompletionAPIRequest), options);
        yield return CaseWithNamespace(typeof(ResponsesAPIRequest), options);
        yield return CaseWithNamespace(typeof(ChatRequest), options);

        //
        // The messages and their parts, which converters write as the type they are of:
        //
        foreach (var type in new[] { typeof(IMessageBase), typeof(ISubContent), typeof(ISubContentImageUrl), typeof(ISubContentImageSource) }.SelectMany(ImplementationsOf))
            yield return CaseWithNamespace(type, options);

        //
        // What a request holds as an object, which gets written as the type it is of, too. The additional API
        // parameters hold the values the parser of the provider settings reads, and the input of the Responses API
        // holds the items of an earlier answer as they came in:
        //
        foreach (var type in new[] { typeof(string), typeof(int), typeof(long), typeof(double), typeof(decimal), typeof(bool), typeof(Dictionary<string, object>), typeof(List<object>), typeof(JsonElement) })
            yield return Case(new(type, options), "value of a request");

        yield return Case(new(ProviderToolAdapters.ToChatCompletionTool(JsonSampleRequests.Tool(), hostEnforcesStrict: true).GetType(), options), "tool of a Chat Completions API");
        yield return CaseWithNamespace(typeof(ResponsesFunctionTool), options);
        yield return CaseWithNamespace(typeof(ResponsesFunctionCallOutputItem), options);
    }

    /// <summary>
    /// What AI Studio receives from LLM providers.
    /// </summary>
    public static IEnumerable<TestCaseData> ProviderAnswers()
    {
        TestHost.PrepareStaticState();
        var options = ProviderJsonOptions.OPTIONS;

        // The lists of models:
        yield return CaseWithNamespace(typeof(AIStudio.Provider.OpenAI.ModelsResponse), options);
        yield return CaseWithNamespace(typeof(AIStudio.Provider.Mistral.ModelsResponse), options);
        yield return CaseWithNamespace(typeof(AIStudio.Provider.SelfHosted.ModelsResponse), options);
        yield return CaseWithNamespace(typeof(AIStudio.Provider.HuggingFace.ModelsResponse), options);
        yield return Case(new(typeof(IList<HubModel>), options), "HuggingFace");
        yield return CaseWithNamespace(typeof(RequestyModelsResponse), options);
        yield return CaseWithNamespace(typeof(OpenRouterModelsResponse), options);
        yield return CaseWithNamespace(typeof(GroqModelsResponse), options);

        // The lines of streamed answers:
        yield return CaseWithNamespace(typeof(ChatCompletionDeltaStreamLine), options);
        yield return CaseWithNamespace(typeof(ChatCompletionAnnotationStreamLine), options);
        yield return CaseWithNamespace(typeof(ChatCompletionToolStreamLine), options);
        yield return CaseWithNamespace(typeof(ResponsesDeltaStreamLine), options);
        yield return CaseWithNamespace(typeof(ResponsesAnnotationStreamLine), options);
        yield return CaseWithNamespace(typeof(ResponsesCompletedStreamLine), options);
        yield return CaseWithNamespace(typeof(AnthropicResponseStreamLine), options);
        yield return CaseWithNamespace(typeof(AnthropicStreamLine), options);
        yield return CaseWithNamespace(typeof(FireworksResponseStreamLine), options);
        yield return CaseWithNamespace(typeof(PerplexityResponseStreamLine), options);

        //
        // The annotations, which a converter reads as the type their JSON tells:
        //
        foreach (var type in ImplementationsOf(typeof(Annotation)))
            yield return CaseWithNamespace(type, options);

        // Transcriptions and embeddings, where a converter reads the embeddings of Google as one or as a list:
        yield return CaseWithNamespace(typeof(TranscriptionResponse), options);
        yield return CaseWithNamespace(typeof(EmbeddingResponse), options);
        yield return CaseWithNamespace(typeof(GoogleEmbeddingResponse), options);
        yield return CaseWithNamespace(typeof(GoogleEmbedding), options);
        yield return Case(new(typeof(List<GoogleEmbedding>), options), "Google");
    }
}