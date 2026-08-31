using System.Runtime.CompilerServices;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.IONOS;

public sealed class ProviderIONOS() : BaseProvider(LLMProviders.IONOS, new Uri("https://openai.inference.de-txl.ionos.com/v1/"), ExternalHttpTrustPolicy.SYSTEM_TRUST_ONLY, LOGGER)
{
    /// <summary>
    /// IONOS keeps an alias of some embedding models around, so that customers can migrate away from
    /// the previous naming. Those aliases point to the very same models we already offer, which is
    /// why we hide them instead of listing every embedding model twice.
    /// </summary>
    private const string MIGRATION_ALIAS_SUFFIX = "-migration";

    private static readonly ILogger<ProviderIONOS> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderIONOS>();

    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.IONOS.ToSecretId();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "IONOS";

    /// <inheritdoc />
    public override bool HasModelLoadingCapability => true;

    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        await foreach (var content in this.StreamOpenAICompatibleChatCompletion<ChatCompletionAPIRequest, ChatCompletionDeltaStreamLine, NoChatCompletionAnnotationStreamLine>(
                           "IONOS",
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

                                   // Right now, we only support streaming completions:
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
    public override Task<TranscriptionResult> TranscribeAudioAsync(Model transcriptionModel, string audioFilePath, SettingsManager settingsManager, CancellationToken token = default)
    {
        return Task.FromResult(TranscriptionResult.Failure());
    }

    /// <inhertidoc />
    public override async Task<IReadOnlyList<IReadOnlyList<float>>> EmbedTextAsync(Model embeddingModel, SettingsManager settingsManager, CancellationToken token = default, params List<string> texts)
    {
        var requestedSecret = await Program.RUST_SERVICE.GetAPIKey(this, SecretStoreType.EMBEDDING_PROVIDER);
        return await this.PerformStandardTextEmbeddingRequest(requestedSecret, embeddingModel, token: token, texts: texts);
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModels(SecretStoreType.LLM_PROVIDER, model => model.IsChatModel(), token, apiKeyProvisional);
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(ModelLoadResult.FromModels([]));
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModels(SecretStoreType.EMBEDDING_PROVIDER, model => model.IsEmbeddingModel(), token, apiKeyProvisional);
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(ModelLoadResult.FromModels([]));
    }

    #endregion

    /// <summary>
    /// Loads the models of one kind from IONOS.
    /// </summary>
    /// <remarks>
    /// IONOS serves chat, embedding, reranking, OCR, and image models through one endpoint, and its
    /// response tells us nothing but the model's name. We therefore let the shared model kind
    /// detection sort them apart.
    /// </remarks>
    /// <param name="storeType">The secret store to read the API key from.</param>
    /// <param name="isWantedKind">Decides whether a model belongs to the requested kind.</param>
    /// <param name="token">The cancellation token.</param>
    /// <param name="apiKeyProvisional">An API key which was not stored yet.</param>
    /// <returns>The models of the requested kind.</returns>
    private Task<ModelLoadResult> LoadModels(SecretStoreType storeType, Func<Model, bool> isWantedKind, CancellationToken token, string? apiKeyProvisional = null)
    {
        return this.LoadModelsResponse<ModelsResponse>(
            storeType,
            "models",
            modelResponse => modelResponse.Data
                .Where(model => !model.Id.EndsWith(MIGRATION_ALIAS_SUFFIX, StringComparison.OrdinalIgnoreCase))
                .Where(isWantedKind),
            token,
            apiKeyProvisional);
    }
}