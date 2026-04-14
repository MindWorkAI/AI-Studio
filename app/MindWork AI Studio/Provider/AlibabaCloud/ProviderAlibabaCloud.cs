using System.Runtime.CompilerServices;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.AlibabaCloud;

public sealed class ProviderAlibabaCloud() : BaseProvider(LLMProviders.ALIBABA_CLOUD, "https://dashscope-intl.aliyuncs.com/compatible-mode/v1/", LOGGER)
{
    private static readonly ILogger<ProviderAlibabaCloud> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderAlibabaCloud>();

    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.ALIBABA_CLOUD.ToName();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "AlibabaCloud";
    
    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        await foreach (var content in this.StreamOpenAICompatibleChatCompletion<ChatCompletionAPIRequest, ChatCompletionDeltaStreamLine, NoChatCompletionAnnotationStreamLine>(
                           "AlibabaCloud",
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
        return await this.PerformStandardTextEmbeddingRequest(requestedSecret, embeddingModel, token: token, texts: texts);
    }

    /// <inheritdoc />
    public override async Task<ModelLoadResult> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var additionalModels = new[]
        {
            new Model("qwq-plus", "QwQ plus"), // reasoning model 
            new Model("qwen-max-latest", "Qwen-Max (Latest)"),
            new Model("qwen-plus-latest", "Qwen-Plus (Latest)"),
            new Model("qwen-turbo-latest", "Qwen-Turbo (Latest)"),
            new Model("qvq-max", "QVQ Max"), // visual reasoning model 
            new Model("qvq-max-latest", "QVQ Max (Latest)"), // visual reasoning model 
            new Model("qwen-vl-max", "Qwen-VL Max"), // text generation model that can understand and process images
            new Model("qwen-vl-plus", "Qwen-VL Plus"), // text generation model that can understand and process images
            new Model("qwen-mt-plus", "Qwen-MT Plus"), // machine translation
            new Model("qwen-mt-turbo", "Qwen-MT Turbo"), // machine translation
            
            //Open source
            new Model("qwen2.5-14b-instruct-1m", "Qwen2.5 14b 1m context"), 
            new Model("qwen2.5-7b-instruct-1m", "Qwen2.5 7b 1m context"),
            new Model("qwen2.5-72b-instruct", "Qwen2.5 72b"),  
            new Model("qwen2.5-32b-instruct", "Qwen2.5 32b"),  
            new Model("qwen2.5-14b-instruct", "Qwen2.5 14b"),  
            new Model("qwen2.5-7b-instruct", "Qwen2.5 7b"),  
            new Model("qwen2.5-omni-7b", "Qwen2.5-Omni 7b"), // omni-modal understanding and generation model
            new Model("qwen2.5-vl-72b-instruct", "Qwen2.5-VL 72b"),  
            new Model("qwen2.5-vl-32b-instruct", "Qwen2.5-VL 32b"),  
            new Model("qwen2.5-vl-7b-instruct", "Qwen2.5-VL 7b"),  
            new Model("qwen2.5-vl-3b-instruct", "Qwen2.5-VL 3b"),  
        };
        
        var result = await this.LoadModels(["q"], SecretStoreType.LLM_PROVIDER, token, apiKeyProvisional);
        return result with
        {
            Models = [..result.Models.Concat(additionalModels).OrderBy(x => x.Id)]
        };
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(ModelLoadResult.FromModels([]));
    }
    
    /// <inheritdoc />
    public override async Task<ModelLoadResult> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        
        var additionalModels = new[]
        {
            new Model("text-embedding-v3", "text-embedding-v3"),
        };
        
        var result = await this.LoadModels(["text-embedding-"], SecretStoreType.EMBEDDING_PROVIDER, token, apiKeyProvisional);
        return result with
        {
            Models = [..result.Models.Concat(additionalModels).OrderBy(x => x.Id)]
        };
    }

    #region Overrides of BaseProvider

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(ModelLoadResult.FromModels([]));
    }

    #endregion

    #endregion
    
    private Task<ModelLoadResult> LoadModels(string[] prefixes, SecretStoreType storeType, CancellationToken token, string? apiKeyProvisional = null)
    {
        return this.LoadModelsResponse<ModelsResponse>(
            storeType,
            "models",
            modelResponse => modelResponse.Data.Where(model => prefixes.Any(prefix => model.Id.StartsWith(prefix, StringComparison.InvariantCulture))),
            token,
            apiKeyProvisional);
    }
    
}