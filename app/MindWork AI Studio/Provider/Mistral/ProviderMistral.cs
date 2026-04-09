using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.Mistral;

public sealed class ProviderMistral() : BaseProvider(LLMProviders.MISTRAL, "https://api.mistral.ai/v1/", LOGGER)
{
    private static readonly ILogger<ProviderMistral> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderMistral>();

    #region Implementation of IProvider

    public override string Id => LLMProviders.MISTRAL.ToName();
    
    public override string InstanceName { get; set; } = "Mistral";

    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Provider.Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        await foreach (var content in this.StreamOpenAICompatibleChatCompletion<ChatCompletionDeltaStreamLine, NoChatCompletionAnnotationStreamLine>(
                           "Mistral",
                           chatModel,
                           chatThread,
                           settingsManager,
                           () => chatThread.Blocks.BuildMessagesUsingDirectImageUrlAsync(this.Provider, chatModel),
                           (systemPrompt, messages, apiParameters, stream, tools) =>
                           {
                               if (TryPopBoolParameter(apiParameters, "safe_prompt", out var parsedSafePrompt))
                                   apiParameters["safe_prompt"] = parsedSafePrompt;

                               if (TryPopIntParameter(apiParameters, "random_seed", out var parsedRandomSeed))
                                   apiParameters["random_seed"] = parsedRandomSeed;

                               return Task.FromResult(new ChatCompletionAPIRequest
                               {
                                   Model = chatModel.Id,
                                   Messages = [systemPrompt, ..messages],
                                   Stream = stream,
                                   Tools = tools,
                                   ParallelToolCalls = tools is null ? null : true,
                                   AdditionalApiParameters = apiParameters
                               });
                           },
                           token: token))
            yield return content;
    }

    #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    /// <inheritdoc />
    public override async IAsyncEnumerable<ImageURL> StreamImageCompletion(Provider.Model imageModel, string promptPositive, string promptNegative = FilterOperator.String.Empty, ImageURL referenceImageURL = default, [EnumeratorCancellation] CancellationToken token = default)
    {
        yield break;
    }
    #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    
    /// <inheritdoc />
    public override async Task<string> TranscribeAudioAsync(Provider.Model transcriptionModel, string audioFilePath, SettingsManager settingsManager, CancellationToken token = default)
    {
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this, SecretStoreType.TRANSCRIPTION_PROVIDER);
        return await this.PerformStandardTranscriptionRequest(requestedSecret, transcriptionModel, audioFilePath, token: token);
    }

    /// <inhertidoc />
    public override async Task<IReadOnlyList<IReadOnlyList<float>>> EmbedTextAsync(Provider.Model embeddingModel, SettingsManager settingsManager, CancellationToken token = default, params List<string> texts)
    {
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this, SecretStoreType.EMBEDDING_PROVIDER);
        return await this.PerformStandardTextEmbeddingRequest(requestedSecret, embeddingModel, token: token, texts: texts);
    }

    /// <inheritdoc />
    public override async Task<IEnumerable<Provider.Model>> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var modelResponse = await this.LoadModelList(SecretStoreType.LLM_PROVIDER, apiKeyProvisional, token);
        if(modelResponse == default)
            return [];
        
        return modelResponse.Data.Where(n => 
            !n.Id.StartsWith("code", StringComparison.OrdinalIgnoreCase) &&
            !n.Id.Contains("embed", StringComparison.OrdinalIgnoreCase) &&
            !n.Id.Contains("moderation", StringComparison.OrdinalIgnoreCase))
            .Select(n => new Provider.Model(n.Id, null));
    }
    
    /// <inheritdoc />
    public override async Task<IEnumerable<Provider.Model>> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var modelResponse = await this.LoadModelList(SecretStoreType.EMBEDDING_PROVIDER, apiKeyProvisional, token);
        if(modelResponse == default)
            return [];
        
        return modelResponse.Data.Where(n => n.Id.Contains("embed", StringComparison.InvariantCulture))
            .Select(n => new Provider.Model(n.Id, null));
    }
    
    /// <inheritdoc />
    public override Task<IEnumerable<Provider.Model>> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(Enumerable.Empty<Provider.Model>());
    }
    
    /// <inheritdoc />
    public override Task<IEnumerable<Provider.Model>> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        // Source: https://docs.mistral.ai/capabilities/audio_transcription
        return Task.FromResult<IEnumerable<Provider.Model>>(
            new List<Provider.Model>
            {
                new("voxtral-mini-latest", "Voxtral Mini Latest"),
            });
    }
    
    #endregion
    
    private async Task<ModelsResponse> LoadModelList(SecretStoreType storeType, string? apiKeyProvisional, CancellationToken token)
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
            return default;
        
        using var request = new HttpRequestMessage(HttpMethod.Get, "models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);

        using var response = await this.httpClient.SendAsync(request, token);
        if(!response.IsSuccessStatusCode)
            return default;

        var modelResponse = await response.Content.ReadFromJsonAsync<ModelsResponse>(token);
        return modelResponse;
    }
}
