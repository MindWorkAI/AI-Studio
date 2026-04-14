using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.OpenRouter;

public sealed class ProviderOpenRouter() : BaseProvider(LLMProviders.OPEN_ROUTER, "https://openrouter.ai/api/v1/", LOGGER)
{
    private const string PROJECT_WEBSITE = "https://github.com/MindWorkAI/AI-Studio";
    private const string PROJECT_NAME = "MindWork AI Studio";

    private static readonly ILogger<ProviderOpenRouter> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderOpenRouter>();

    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.OPEN_ROUTER.ToName();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "OpenRouter";

    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        await foreach (var content in this.StreamOpenAICompatibleChatCompletion<ChatCompletionAPIRequest, ChatCompletionDeltaStreamLine, NoChatCompletionAnnotationStreamLine>(
                           "OpenRouter",
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
                           headersAction: headers =>
                           {
                               // Set custom headers for project identification:
                               headers.Add("HTTP-Referer", PROJECT_WEBSITE);
                               headers.Add("X-Title", PROJECT_NAME);
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
        return await this.PerformStandardTextEmbeddingRequest(requestedSecret, embeddingModel, token: token, texts: texts);
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModels(SecretStoreType.LLM_PROVIDER, token, apiKeyProvisional);
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(ModelLoadResult.FromModels([]));
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadEmbeddingModels(token, apiKeyProvisional);
    }
    
    /// <inheritdoc />
    public override Task<ModelLoadResult> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(ModelLoadResult.FromModels([]));
    }

    #endregion

    private Task<ModelLoadResult> LoadModels(SecretStoreType storeType, CancellationToken token, string? apiKeyProvisional = null)
    {
        return this.LoadModelsResponse<OpenRouterModelsResponse>(
            storeType,
            "models",
            modelResponse => modelResponse.Data
                .Where(n =>
                    !n.Id.Contains("whisper", StringComparison.OrdinalIgnoreCase) &&
                    !n.Id.Contains("dall-e", StringComparison.OrdinalIgnoreCase) &&
                    !n.Id.Contains("tts", StringComparison.OrdinalIgnoreCase) &&
                    !n.Id.Contains("embedding", StringComparison.OrdinalIgnoreCase) &&
                    !n.Id.Contains("moderation", StringComparison.OrdinalIgnoreCase) &&
                    !n.Id.Contains("stable-diffusion", StringComparison.OrdinalIgnoreCase) &&
                    !n.Id.Contains("flux", StringComparison.OrdinalIgnoreCase) &&
                    !n.Id.Contains("midjourney", StringComparison.OrdinalIgnoreCase))
                .Select(n => new Model(n.Id, n.Name)),
            token,
            apiKeyProvisional,
            requestConfigurator: (request, secretKey) =>
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
                request.Headers.Add("HTTP-Referer", PROJECT_WEBSITE);
                request.Headers.Add("X-Title", PROJECT_NAME);
            });
    }

    private Task<ModelLoadResult> LoadEmbeddingModels(CancellationToken token, string? apiKeyProvisional = null)
    {
        return this.LoadModelsResponse<OpenRouterModelsResponse>(
            SecretStoreType.EMBEDDING_PROVIDER,
            "embeddings/models",
            modelResponse => modelResponse.Data.Select(n => new Model(n.Id, n.Name)),
            token,
            apiKeyProvisional,
            requestConfigurator: (request, secretKey) =>
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
                request.Headers.Add("HTTP-Referer", PROJECT_WEBSITE);
                request.Headers.Add("X-Title", PROJECT_NAME);
            });
    }
}