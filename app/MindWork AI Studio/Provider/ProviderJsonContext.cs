using System.Text.Json;
using System.Text.Json.Serialization;

using AIStudio.Provider.Anthropic;
using AIStudio.Provider.Google;
using AIStudio.Provider.Groq;
using AIStudio.Provider.HuggingFace;
using AIStudio.Provider.OpenAI;
using AIStudio.Provider.OpenRouter;
using AIStudio.Provider.Requesty;

using AnthropicDelta = AIStudio.Provider.Anthropic.Delta;
using AnthropicImageUrl = AIStudio.Provider.Anthropic.SubContentImageUrl;
using AnthropicResponseStreamLine = AIStudio.Provider.Anthropic.ResponseStreamLine;
using FireworksChoice = AIStudio.Provider.Fireworks.Choice;
using FireworksDelta = AIStudio.Provider.Fireworks.Delta;
using FireworksResponseStreamLine = AIStudio.Provider.Fireworks.ResponseStreamLine;
using HuggingFaceModelsResponse = AIStudio.Provider.HuggingFace.ModelsResponse;
using MistralModel = AIStudio.Provider.Mistral.Model;
using MistralModelsResponse = AIStudio.Provider.Mistral.ModelsResponse;
using OpenAIImageUrl = AIStudio.Provider.OpenAI.SubContentImageUrl;
using OpenAIModelsResponse = AIStudio.Provider.OpenAI.ModelsResponse;
using PerplexityChoice = AIStudio.Provider.Perplexity.Choice;
using PerplexityDelta = AIStudio.Provider.Perplexity.Delta;
using PerplexityResponseStreamLine = AIStudio.Provider.Perplexity.ResponseStreamLine;
using SelfHostedModel = AIStudio.Provider.SelfHosted.Model;
using SelfHostedModelsResponse = AIStudio.Provider.SelfHosted.ModelsResponse;

namespace AIStudio.Provider;

/// <summary>
/// The contracts with which System.Text.Json reads and writes the requests and answers of LLM providers, built
/// at compile time.
/// </summary>
/// <remarks>
/// Some types System.Text.Json meets only while it runs, since no property names them: the messages and their
/// parts, which converters write as the type they are of; the annotations, which a converter reads as the type
/// their JSON tells; and whatever a request holds as an object, i.e., the values of the additional API
/// parameters, the tools, and the items of an earlier answer. A test fails for such a type which is missing
/// here.<br/><br/>
///
/// Two types with the same name need a name of their own for their contract, else the generator builds only
/// the first of them and warns with SYSLIB1031.
/// </remarks>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]

// The requests:
[JsonSerializable(typeof(ChatCompletionAPIRequest))]
[JsonSerializable(typeof(ResponsesAPIRequest))]
[JsonSerializable(typeof(ChatRequest))]
[JsonSerializable(typeof(EmbeddingRequest))]
[JsonSerializable(typeof(GoogleEmbeddingRequest))]

// The messages and their parts:
[JsonSerializable(typeof(TextMessage))]
[JsonSerializable(typeof(MultimodalMessage))]
[JsonSerializable(typeof(AssistantToolCallMessage))]
[JsonSerializable(typeof(ToolResultMessage))]
[JsonSerializable(typeof(AnthropicMessage))]
[JsonSerializable(typeof(AnthropicToolResultMessage))]
[JsonSerializable(typeof(SubContentText))]
[JsonSerializable(typeof(OpenAIImageUrl), TypeInfoPropertyName = "OpenAISubContentImageUrl")]
[JsonSerializable(typeof(SubContentImageUrlNested))]
[JsonSerializable(typeof(SubContentImageUrlData))]
[JsonSerializable(typeof(SubContentInputText))]
[JsonSerializable(typeof(SubContentInputImage))]
[JsonSerializable(typeof(SubContentImage))]
[JsonSerializable(typeof(SubContentBase64Image))]
[JsonSerializable(typeof(AnthropicImageUrl), TypeInfoPropertyName = "AnthropicSubContentImageUrl")]

// What a request holds as an object:
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(List<object>))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(ChatCompletionFunctionTool))]
[JsonSerializable(typeof(ResponsesFunctionTool))]
[JsonSerializable(typeof(ResponsesFunctionCallOutputItem))]

// The lists of models:
[JsonSerializable(typeof(OpenAIModelsResponse), TypeInfoPropertyName = "OpenAIModelsResponse")]
[JsonSerializable(typeof(MistralModelsResponse), TypeInfoPropertyName = "MistralModelsResponse")]
[JsonSerializable(typeof(SelfHostedModelsResponse), TypeInfoPropertyName = "SelfHostedModelsResponse")]
[JsonSerializable(typeof(HuggingFaceModelsResponse), TypeInfoPropertyName = "HuggingFaceModelsResponse")]
[JsonSerializable(typeof(IList<HubModel>))]
[JsonSerializable(typeof(RequestyModelsResponse))]
[JsonSerializable(typeof(OpenRouterModelsResponse))]
[JsonSerializable(typeof(GroqModelsResponse))]

// The lines of streamed answers, with the annotations:
[JsonSerializable(typeof(ChatCompletionDeltaStreamLine))]
[JsonSerializable(typeof(ChatCompletionAnnotationStreamLine))]
[JsonSerializable(typeof(ChatCompletionToolStreamLine))]
[JsonSerializable(typeof(ResponsesDeltaStreamLine))]
[JsonSerializable(typeof(ResponsesAnnotationStreamLine))]
[JsonSerializable(typeof(ResponsesCompletedStreamLine))]
[JsonSerializable(typeof(AnthropicResponseStreamLine), TypeInfoPropertyName = "AnthropicResponseStreamLine")]
[JsonSerializable(typeof(AnthropicStreamLine))]
[JsonSerializable(typeof(FireworksResponseStreamLine), TypeInfoPropertyName = "FireworksResponseStreamLine")]
[JsonSerializable(typeof(PerplexityResponseStreamLine), TypeInfoPropertyName = "PerplexityResponseStreamLine")]
[JsonSerializable(typeof(AnnotatingUnknown))]
[JsonSerializable(typeof(ChatCompletionAnnotatingURL))]
[JsonSerializable(typeof(ResponsesAnnotatingUrlCitationData))]

// Transcriptions and embeddings:
[JsonSerializable(typeof(TranscriptionResponse))]
[JsonSerializable(typeof(EmbeddingResponse))]
[JsonSerializable(typeof(GoogleEmbeddingResponse))]
[JsonSerializable(typeof(GoogleEmbedding))]
[JsonSerializable(typeof(List<GoogleEmbedding>))]

// The types inside the answers which share their names with others:
[JsonSerializable(typeof(MistralModel), TypeInfoPropertyName = "MistralModel")]
[JsonSerializable(typeof(MistralModel[]), TypeInfoPropertyName = "MistralModelArray")]
[JsonSerializable(typeof(SelfHostedModel), TypeInfoPropertyName = "SelfHostedModel")]
[JsonSerializable(typeof(SelfHostedModel[]), TypeInfoPropertyName = "SelfHostedModelArray")]
[JsonSerializable(typeof(FireworksChoice), TypeInfoPropertyName = "FireworksChoice")]
[JsonSerializable(typeof(IList<FireworksChoice>), TypeInfoPropertyName = "IListFireworksChoice")]
[JsonSerializable(typeof(PerplexityChoice), TypeInfoPropertyName = "PerplexityChoice")]
[JsonSerializable(typeof(IList<PerplexityChoice>), TypeInfoPropertyName = "IListPerplexityChoice")]
[JsonSerializable(typeof(AnthropicDelta), TypeInfoPropertyName = "AnthropicDelta")]
[JsonSerializable(typeof(FireworksDelta), TypeInfoPropertyName = "FireworksDelta")]
[JsonSerializable(typeof(PerplexityDelta), TypeInfoPropertyName = "PerplexityDelta")]
internal sealed partial class ProviderJsonContext : JsonSerializerContext;