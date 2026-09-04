using System.Runtime.CompilerServices;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.GWDG;

public sealed class ProviderGWDG() : BaseProvider(LLMProviders.GWDG, new Uri("https://chat-ai.academiccloud.de/v1/"), ExternalHttpTrustPolicy.SYSTEM_TRUST_ONLY, LOGGER)
{
    private static readonly ILogger<ProviderGWDG> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderGWDG>();

    // Source: https://docs.hpc.gwdg.de/services/saia/index.html#embeddings
    private static readonly Model[] KNOWN_EMBEDDING_MODELS =
    [
        new("e5-mistral-7b-instruct", "E5 Mistral 7B Instruct"),
        new("multilingual-e5-large-instruct", "Multilingual E5 Large Instruct"),
        new("qwen3-embedding-4b", "Qwen3 Embedding 4B"),
    ];

    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.GWDG.ToSecretId();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "GWDG SAIA";

    /// <inheritdoc />
    public override bool HasModelLoadingCapability => true;
    
    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        await foreach (var content in this.StreamOpenAICompatibleChatCompletion<ChatCompletionAPIRequest, ChatCompletionDeltaStreamLine, ChatCompletionAnnotationStreamLine>(
                           "GWDG",
                           chatModel,
                           chatThread,
                           settingsManager,
                           async (systemPrompt, apiParameters, tools) =>
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

                                   Stream = true,
                                   Tools = tools,
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
    public override async Task<TranscriptionResult> TranscribeAudioAsync(Model transcriptionModel, string audioFilePath, SettingsManager settingsManager, CancellationToken token = default)
    {
        var requestedSecret = await Program.RUST_SERVICE.GetAPIKey(this, SecretStoreType.TRANSCRIPTION_PROVIDER);
        return await this.PerformStandardTranscriptionRequest(requestedSecret, transcriptionModel, audioFilePath, token: token);
    }
    
    /// <inhertidoc />
    public override async Task<IReadOnlyList<IReadOnlyList<float>>> EmbedTextAsync(Model embeddingModel, SettingsManager settingsManager, CancellationToken token = default, params List<string> texts)
    {
        var requestedSecret = await Program.RUST_SERVICE.GetAPIKey(this, SecretStoreType.EMBEDDING_PROVIDER);
        return await this.PerformStandardTextEmbeddingRequest(requestedSecret, embeddingModel, token: token, texts: texts);
    }

    /// <inheritdoc />
    public override async Task<ModelLoadResult> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var result = await this.LoadModels(SecretStoreType.LLM_PROVIDER, apiKeyProvisional, token);
        return result with
        {
            Models = [..result.Models.Where(model => model.IsChatModel())]
        };
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(ModelLoadResult.FromModels([]));
    }
    
    /// <inheritdoc />
    /// <remarks>
    /// SAIA answers the models endpoint with its chat models only, so asking it for the embedding
    /// models comes back empty. We therefore fall back to the models the documentation names. The
    /// endpoint is still asked first: should SAIA start reporting them one day, its answer wins
    /// over our list. A failed request is passed on unchanged, so a wrong API key stays visible
    /// as such instead of being covered up by the fallback.
    /// </remarks>
    public override async Task<ModelLoadResult> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var result = await this.LoadModels(SecretStoreType.EMBEDDING_PROVIDER, apiKeyProvisional, token);
        if (!result.Success)
            return result;

        var embeddingModels = result.Models.Where(model => model.IsEmbeddingModel()).ToList();
        if (embeddingModels.Count is 0)
            return ModelLoadResult.FromModels(KNOWN_EMBEDDING_MODELS);

        return result with
        {
            Models = [..embeddingModels]
        };
    }
    
    /// <inheritdoc />
    public override Task<ModelLoadResult> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        // Source: https://docs.hpc.gwdg.de/services/saia/index.html#voice-to-text
        return Task.FromResult(ModelLoadResult.FromModels(
        [
            new Model("whisper-large-v2", "Whisper v2 Large"),
        ]));
    }
    
    #endregion

    private async Task<ModelLoadResult> LoadModels(SecretStoreType storeType, string? apiKeyProvisional, CancellationToken token)
    {
        var result = await this.LoadModelsResponse<ModelsResponse>(
            storeType,
            "models",
            modelResponse => modelResponse.Data,
            apiKeyProvisional, token: token);

        if (!result.Success)
            LOGGER.LogWarning("Failed to load models for provider {ProviderId}. FailureReason: {FailureReason}. TechnicalDetails: {TechnicalDetails}", this.Id, result.FailureReason, result.TechnicalDetails);

        return result;
    }
}