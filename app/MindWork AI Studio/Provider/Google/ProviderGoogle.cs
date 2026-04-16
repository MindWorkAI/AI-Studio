using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.Google;

public class ProviderGoogle() : BaseProvider(LLMProviders.GOOGLE, "https://generativelanguage.googleapis.com/v1beta/openai/", LOGGER)
{
    private static readonly ILogger<ProviderGoogle> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderGoogle>();

    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.GOOGLE.ToName();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "Google Gemini";

    /// <inheritdoc />
    public override bool HasModelLoadingCapability => true;

    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        await foreach (var content in this.StreamOpenAICompatibleChatCompletion<ChatCompletionAPIRequest, ChatCompletionDeltaStreamLine, NoChatCompletionAnnotationStreamLine>(
                           "Google",
                           chatModel,
                           chatThread,
                           settingsManager,
                           async (systemPrompt, apiParameters) =>
                           {
                               // Build the list of messages:
                               var messages = await chatThread.Blocks.BuildMessagesUsingNestedImageUrlAsync(this.Provider, chatModel);

                               return new ChatCompletionAPIRequest
                               {
                                   Model = chatModel.Id,

                                   // Build the messages:
                                   // - First of all the system prompt
                                   // - Then none-empty user and AI messages
                                   Messages = [systemPrompt, ..messages],

                                   // Right now, we only support streaming completions:
                                   Stream = true,
                                   AdditionalApiParameters = apiParameters
                               };
                           },
                           token: token))
            yield return content;
    }

    #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    /// <inheritdoc />
    public override async IAsyncEnumerable<ImageURL> StreamImageCompletion(Model imageModel, string promptPositive, string promptNegative = FilterOperator.String.Empty, ImageURL referenceImageURL = default, [EnumeratorCancellation] CancellationToken token = default)
    {
        yield break;
    }
    #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

    /// <inheritdoc />
    public override Task<string> TranscribeAudioAsync(Model transcriptionModel, string audioFilePath, SettingsManager settingsManager, CancellationToken token = default)
    {
        return Task.FromResult(string.Empty);
    }
    
    /// <inhertidoc />
    public override async Task<IReadOnlyList<IReadOnlyList<float>>> EmbedTextAsync(Model embeddingModel, SettingsManager settingsManager, CancellationToken token = default, params List<string> texts)
    {
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this, SecretStoreType.EMBEDDING_PROVIDER);
        try
        {
            var modelName = embeddingModel.Id;
            if (string.IsNullOrWhiteSpace(modelName))
            {
                LOGGER.LogError("No model name provided for embedding request.");
                return [];
            }

            if (modelName.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
                modelName = modelName.Substring("models/".Length);

            if (!requestedSecret.Success)
            {
                LOGGER.LogError("No valid API key available for embedding request.");
                return [];
            }
            
            // Prepare the Google Gemini embedding request:
            var payload = new
            {
                content = new
                {
                    parts = texts.Select(text => new { text }).ToArray()
                },
                
                taskType = "SEMANTIC_SIMILARITY"
            };
            
            var embeddingRequest = JsonSerializer.Serialize(payload, JSON_SERIALIZER_OPTIONS);
            var embedUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:embedContent";
            using var request = new HttpRequestMessage(HttpMethod.Post, embedUrl);
            request.Headers.Add("x-goog-api-key", await requestedSecret.Secret.Decrypt(ENCRYPTION));
            
            // Set the content:
            request.Content = new StringContent(embeddingRequest, Encoding.UTF8, "application/json");
            
            using var response = await this.HttpClient.SendAsync(request, token);
            var responseBody = await response.Content.ReadAsStringAsync(token);
        
            if (!response.IsSuccessStatusCode)
            {
                LOGGER.LogError("Embedding request failed with status code {ResponseStatusCode} and body: '{ResponseBody}'.", response.StatusCode, responseBody);
                return [];
            }

            var embeddingResponse = JsonSerializer.Deserialize<GoogleEmbeddingResponse>(responseBody, JSON_SERIALIZER_OPTIONS);
            if (embeddingResponse is { Embedding: not null })
            {
                return embeddingResponse.Embedding
                    .Select(d => d.Values?.ToArray() ?? [])
                    .Cast<IReadOnlyList<float>>()
                    .ToArray();
            }
            else
            {
                LOGGER.LogError("Was not able to deserialize the embedding response.");
                return [];
            }
            
        }
        catch (Exception e)
        {
            LOGGER.LogError("Failed to perform embedding request: '{Message}'.", e.Message);
            return [];
        }
    }

    /// <inheritdoc />
    public override async Task<ModelLoadResult> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var result = await this.LoadModels(SecretStoreType.LLM_PROVIDER, token, apiKeyProvisional);
        return result with
        {
            Models =
            [
                ..result.Models.Where(model =>
                        model.Id.StartsWith("gemini-", StringComparison.OrdinalIgnoreCase) &&
                        !this.IsEmbeddingModel(model.Id))
                    .Select(this.WithDisplayNameFallback)
            ]
        };
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(ModelLoadResult.FromModels([]));
    }

    public override async Task<ModelLoadResult> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var result = await this.LoadModels(SecretStoreType.EMBEDDING_PROVIDER, token, apiKeyProvisional);
        return result with
        {
            Models =
            [
                ..result.Models.Where(model => this.IsEmbeddingModel(model.Id))
                    .Select(this.WithDisplayNameFallback)
            ]
        };
    }
    
    /// <inheritdoc />
    public override Task<ModelLoadResult> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(ModelLoadResult.FromModels([]));
    }
    
    #endregion

    private Task<ModelLoadResult> LoadModels(SecretStoreType storeType, CancellationToken token, string? apiKeyProvisional = null)
    {
        return this.LoadModelsResponse<ModelsResponse>(
            storeType,
            "models",
            modelResponse => modelResponse.Data
                .Where(model => !string.IsNullOrWhiteSpace(model.Id))
                .Select(model => new Model(this.NormalizeModelId(model.Id), model.DisplayName)),
            token,
            apiKeyProvisional,
            failureReasonSelector: (response, _) => response.StatusCode switch
            {
                System.Net.HttpStatusCode.Forbidden => ModelLoadFailureReason.AUTHENTICATION_OR_PERMISSION_ERROR,
                System.Net.HttpStatusCode.Unauthorized => ModelLoadFailureReason.INVALID_OR_MISSING_API_KEY,
                _ => ModelLoadFailureReason.PROVIDER_UNAVAILABLE,
            });
    }

    private bool IsEmbeddingModel(string modelId)
    {
        return modelId.Contains("embedding", StringComparison.OrdinalIgnoreCase) ||
               modelId.Contains("embed", StringComparison.OrdinalIgnoreCase);
    }

    private Model WithDisplayNameFallback(Model model)
    {
        return string.IsNullOrWhiteSpace(model.DisplayName)
            ? new Model(model.Id, model.Id)
            : model;
    }

    private string NormalizeModelId(string modelId)
    {
        return modelId.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? modelId["models/".Length..]
            : modelId;
    }
}
