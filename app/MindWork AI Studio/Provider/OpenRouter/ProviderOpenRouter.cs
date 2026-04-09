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
        await foreach (var content in this.StreamOpenAICompatibleChatCompletion<ChatCompletionDeltaStreamLine, NoChatCompletionAnnotationStreamLine>(
                           "OpenRouter",
                           chatModel,
                           chatThread,
                           settingsManager,
                           () => chatThread.Blocks.BuildMessagesUsingNestedImageUrlAsync(this.Provider, chatModel),
                           (systemPrompt, messages, apiParameters, stream, tools) =>
                               Task.FromResult(new ChatCompletionAPIRequest
                               {
                                   Model = chatModel.Id,
                                   Messages = [systemPrompt, ..messages],
                                   Stream = stream,
                                   Tools = tools,
                                   ParallelToolCalls = tools is null ? null : true,
                                   AdditionalApiParameters = apiParameters
                               }),
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
    public override Task<IEnumerable<Model>> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModels(SecretStoreType.LLM_PROVIDER, token, apiKeyProvisional);
    }

    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(Enumerable.Empty<Model>());
    }

    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadEmbeddingModels(token, apiKeyProvisional);
    }
    
    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(Enumerable.Empty<Model>());
    }

    #endregion

    private async Task<IEnumerable<Model>> LoadModels(SecretStoreType storeType, CancellationToken token, string? apiKeyProvisional = null)
    {
        var secretKey = apiKeyProvisional switch
        {
            not null => apiKeyProvisional,
            _ => await RUST_SERVICE.GetAPIKey(this, storeType) switch
            {
                { Success: true } result => await result.Secret.Decrypt(ENCRYPTION),
                _ => null,
            }
        };

        if (secretKey is null)
            return [];

        using var request = new HttpRequestMessage(HttpMethod.Get, "models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
        
        // Set custom headers for project identification:
        request.Headers.Add("HTTP-Referer", PROJECT_WEBSITE);
        request.Headers.Add("X-Title", PROJECT_NAME);

        using var response = await this.httpClient.SendAsync(request, token);
        if(!response.IsSuccessStatusCode)
            return [];

        var modelResponse = await response.Content.ReadFromJsonAsync<OpenRouterModelsResponse>(token);

        // Filter out non-text models (image, audio, embedding models) and convert to Model
        return modelResponse.Data
            .Where(n =>
                !n.Id.Contains("whisper", StringComparison.OrdinalIgnoreCase) &&
                !n.Id.Contains("dall-e", StringComparison.OrdinalIgnoreCase) &&
                !n.Id.Contains("tts", StringComparison.OrdinalIgnoreCase) &&
                !n.Id.Contains("embedding", StringComparison.OrdinalIgnoreCase) &&
                !n.Id.Contains("moderation", StringComparison.OrdinalIgnoreCase) &&
                !n.Id.Contains("stable-diffusion", StringComparison.OrdinalIgnoreCase) &&
                !n.Id.Contains("flux", StringComparison.OrdinalIgnoreCase) &&
                !n.Id.Contains("midjourney", StringComparison.OrdinalIgnoreCase))
            .Select(n => new Model(n.Id, n.Name));
    }

    private async Task<IEnumerable<Model>> LoadEmbeddingModels(CancellationToken token, string? apiKeyProvisional = null)
    {
        var secretKey = apiKeyProvisional switch
        {
            not null => apiKeyProvisional,
            _ => await RUST_SERVICE.GetAPIKey(this, SecretStoreType.EMBEDDING_PROVIDER) switch
            {
                { Success: true } result => await result.Secret.Decrypt(ENCRYPTION),
                _ => null,
            }
        };

        if (secretKey is null)
            return [];

        using var request = new HttpRequestMessage(HttpMethod.Get, "embeddings/models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
        
        // Set custom headers for project identification:
        request.Headers.Add("HTTP-Referer", PROJECT_WEBSITE);
        request.Headers.Add("X-Title", PROJECT_NAME);

        using var response = await this.httpClient.SendAsync(request, token);
        if(!response.IsSuccessStatusCode)
            return [];

        var modelResponse = await response.Content.ReadFromJsonAsync<OpenRouterModelsResponse>(token);

        // Convert all embedding models to Model
        return modelResponse.Data.Select(n => new Model(n.Id, n.Name));
    }
}
