using System.Runtime.CompilerServices;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.HuggingFace;

public sealed class ProviderHuggingFace : BaseProvider
{
    private static readonly ILogger<ProviderHuggingFace> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderHuggingFace>();

    /// <summary>
    /// The OpenAI-compatible endpoint which serves every inference provider.
    /// </summary>
    /// <remarks>
    /// Hugging Face also keeps a route per provider, such as "/novita/v3/openai/". Those expect the
    /// model ID as that provider spells it, which differs from the ID on the hub: Novita knows
    /// "google/gemma-4-31B-it" as "google/gemma-4-31b-it", and the router is case-sensitive. Asking
    /// for the hub spelling there is answered with "Model not supported by provider novita". This
    /// endpoint takes the hub spelling and translates it for us, so it is the one we use.
    /// </remarks>
    private const string ROUTER_BASE_URL = "https://router.huggingface.co/v1/";

    private readonly HFInferenceProvider hfProvider;

    public ProviderHuggingFace(HFInferenceProvider hfProvider) : base(LLMProviders.HUGGINGFACE, new Uri(ROUTER_BASE_URL), ExternalHttpTrustPolicy.SYSTEM_TRUST_ONLY, LOGGER)
    {
        this.hfProvider = hfProvider;
        LOGGER.LogInformation($"We use the inference provider '{hfProvider}'. Thus, we address the models as '<model>{hfProvider.ModelSuffix()}'.");
    }

    /// <summary>
    /// Builds the model name to send to the router.
    /// </summary>
    /// <remarks>
    /// The router picks the inference provider from a suffix on the model name. When the user wrote
    /// a suffix themselves, we keep theirs: appending a second one would name a model nobody knows.
    /// </remarks>
    /// <param name="model">The model the user chose.</param>
    /// <returns>The model name including the provider suffix, when one applies.</returns>
    private string BuildModelIdentifier(Model model)
    {
        var modelId = model.Id;
        if (string.IsNullOrWhiteSpace(modelId) || modelId.Contains(':'))
            return modelId;

        return $"{modelId}{this.hfProvider.ModelSuffix()}";
    }

    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.HUGGINGFACE.ToSecretId();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "HuggingFace";

    /// <inheritdoc />
    public override bool HasModelLoadingCapability => false;

    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        await foreach (var content in this.StreamOpenAICompatibleChatCompletion<ChatCompletionAPIRequest, ChatCompletionDeltaStreamLine, ChatCompletionAnnotationStreamLine>(
                           "HuggingFace",
                           chatModel,
                           chatThread,
                           settingsManager,
                           async (systemPrompt, apiParameters) =>
                           {
                               // Build the list of messages:
                               var messages = await chatThread.Blocks.BuildMessagesUsingNestedImageUrlAsync(this.Provider, chatModel);

                               return new ChatCompletionAPIRequest
                               {
                                   Model = this.BuildModelIdentifier(chatModel),

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
    public override Task<TranscriptionResult> TranscribeAudioAsync(Model transcriptionModel, string audioFilePath, SettingsManager settingsManager, CancellationToken token = default)
    {
        return Task.FromResult(TranscriptionResult.Failure());
    }
    
    /// <inhertidoc />
    public override Task<IReadOnlyList<IReadOnlyList<float>>> EmbedTextAsync(Model embeddingModel, SettingsManager settingsManager, CancellationToken token = default, params List<string> texts)
    {
        return Task.FromResult<IReadOnlyList<IReadOnlyList<float>>>([]);
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(ModelLoadResult.FromModels([]));
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(ModelLoadResult.FromModels([]));
    }
    
    /// <inheritdoc />
    public override Task<ModelLoadResult> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(ModelLoadResult.FromModels([]));
    }
    
    /// <inheritdoc />
    public override Task<ModelLoadResult> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(ModelLoadResult.FromModels([]));
    }
    
    #endregion
}
