using System.Runtime.CompilerServices;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.AlibabaCloud;

public sealed class ProviderAlibabaCloud() : BaseProvider(LLMProviders.ALIBABA_CLOUD, new Uri("https://dashscope-intl.aliyuncs.com/compatible-mode/v1/"), ExternalHttpTrustPolicy.SYSTEM_TRUST_ONLY, LOGGER)
{
    private static readonly ILogger<ProviderAlibabaCloud> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderAlibabaCloud>();

    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.ALIBABA_CLOUD.ToSecretId();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "AlibabaCloud";

    /// <inheritdoc />
    public override bool HasModelLoadingCapability => true;
    
    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        await foreach (var content in this.StreamOpenAICompatibleChatCompletion<ChatCompletionAPIRequest, ChatCompletionDeltaStreamLine, NoChatCompletionAnnotationStreamLine>(
                           "AlibabaCloud",
                           chatModel,
                           chatThread,
                           settingsManager,
                           async (systemPrompt, apiParameters, tools) =>
                           {
                               // Build the list of messages:
                               var messages = await chatThread.Blocks.BuildMessagesUsingNestedImageUrlAsync(this.CreateSettingsProvider(chatModel));

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
    public override async Task<ModelLoadResult> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var result = await this.LoadModels(SecretStoreType.LLM_PROVIDER, apiKeyProvisional, token);
        return result with { Models = [..result.Models.Where(model => model.IsChatModel(this.Provider)).OrderBy(x => x.Id)] };
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(ModelLoadResult.FromModels([]));
    }
    
    /// <inheritdoc />
    public override async Task<ModelLoadResult> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var result = await this.LoadModels(SecretStoreType.EMBEDDING_PROVIDER, apiKeyProvisional, token);
        return result with { Models = [..result.Models.Where(model => model.IsEmbeddingModel(this.Provider)).OrderBy(x => x.Id)] };
    }

    #region Overrides of BaseProvider

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(ModelLoadResult.FromModels([]));
    }

    #endregion

    #endregion
    
    /// <summary>
    /// Reads Model Studio's catalog, whole.
    /// </summary>
    /// <remarks>
    /// It used to be read through a prefix per list -- a single "q" for the models to talk to, and
    /// "text-embedding-" for the ones which answer in vectors. Neither survived what Model Studio
    /// became: the letter also brings qwen-image, qwen-tts, qwen3-asr and qwen-vl-ocr into the chat
    /// list, while it locks out DeepSeek, Kimi, GLM and MiniMax, which Alibaba serves through this
    /// very endpoint. A name has never been a statement about what a model is for; the callers ask
    /// the registry instead.
    /// </remarks>
    private Task<ModelLoadResult> LoadModels(SecretStoreType storeType, string? apiKeyProvisional, CancellationToken token)
    {
        return this.LoadModelsResponse<ModelsResponse>(
            storeType,
            "models",
            modelResponse => modelResponse.Data,
            apiKeyProvisional, token: token);
    }
}