using System.Net;
using System.Runtime.CompilerServices;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Provider.HuggingFace;

public sealed class ProviderHuggingFace : BaseProvider
{
    private static readonly ILogger<ProviderHuggingFace> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderHuggingFace>();

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ProviderHuggingFace).Namespace, nameof(ProviderHuggingFace));

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

    /// <summary>
    /// Where the models of an inference provider are listed.
    /// </summary>
    /// <remarks>
    /// The router lists the chat models it routes, but nothing else. Which embedding models a
    /// provider offers is known to the hub alone, which answers this without a token. The URL is
    /// absolute on purpose: it addresses the hub, not the router this provider is built on.
    /// </remarks>
    private const string HUB_MODELS_URL = "https://huggingface.co/api/models?limit=100&sort=downloads&direction=-1&inference_provider=";

    private readonly HFInferenceProvider hfProvider;

    public ProviderHuggingFace(HFInferenceProvider hfProvider, HFEndpointKind endpointKind = HFEndpointKind.CHAT) : base(LLMProviders.HUGGINGFACE, new Uri(BuildBaseURL(hfProvider, endpointKind)), ExternalHttpTrustPolicy.SYSTEM_TRUST_ONLY, LOGGER)
    {
        this.hfProvider = hfProvider;
        LOGGER.LogInformation($"We use the inference provider '{hfProvider}' for {endpointKind}. Thus, we use the base URL '{BuildBaseURL(hfProvider, endpointKind)}'.");
    }

    /// <summary>
    /// Determines the base URL for the endpoint this provider instance talks to.
    /// </summary>
    /// <remarks>
    /// A provider which serves no embeddings has no route of its own to offer, and neither have the
    /// routing strategies. We still have to hand a URL to the base class, so we fall back to the
    /// router. A request sent there is answered with a plain "Not Found", which is the honest
    /// outcome: the user selected something we told them we cannot do, and the validation of the
    /// dialog says so before it ever comes to a request.
    /// </remarks>
    /// <param name="hfProvider">The chosen inference provider.</param>
    /// <param name="endpointKind">Which endpoint this instance is built for.</param>
    /// <returns>The base URL to use.</returns>
    private static string BuildBaseURL(HFInferenceProvider hfProvider, HFEndpointKind endpointKind)
    {
        if (endpointKind is HFEndpointKind.CHAT)
            return ROUTER_BASE_URL;

        var providerBaseURL = hfProvider.ProviderBaseURL();
        return string.IsNullOrEmpty(providerBaseURL) ? ROUTER_BASE_URL : providerBaseURL;
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

    /// <summary>
    /// Recognizes the router's answer for a model the chosen inference provider does not serve.
    /// </summary>
    /// <remarks>
    /// Not every model is available at every inference provider, and the router says so with a bad
    /// request. Without this, the user would be told that the message format might have changed,
    /// which points them at something they cannot fix and away from the one thing they can: picking
    /// another provider. The router words this failure as the error code "model_not_supported",
    /// while the providers behind it word it as a sentence of their own.
    /// </remarks>
    /// <param name="value">A piece of the failed response: an error code, a message, or the body.</param>
    /// <returns>True, when this text names an unsupported model.</returns>
    private static bool IsModelNotSupportedError(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Contains("model_not_supported", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("not supported by provider", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("not supported by any provider", StringComparison.OrdinalIgnoreCase);
    }

    #region Overrides of BaseProvider

    /// <inheritdoc />
    protected override ProviderRequestFailureReason ClassifyProviderRequestFailure(HttpStatusCode statusCode, string responseBody)
    {
        if (statusCode is HttpStatusCode.BadRequest && IsModelNotSupportedError(responseBody))
            return ProviderRequestFailureReason.MODEL_NOT_SUPPORTED_BY_PROVIDER;

        return base.ClassifyProviderRequestFailure(statusCode, responseBody);
    }

    /// <inheritdoc />
    protected override ProviderRequestFailureReason ClassifyProviderRequestFailure(string? errorCode, string? errorType, string? errorMessage, string responseBody)
    {
        if (IsModelNotSupportedError(errorCode) || IsModelNotSupportedError(errorType) || IsModelNotSupportedError(errorMessage))
            return ProviderRequestFailureReason.MODEL_NOT_SUPPORTED_BY_PROVIDER;

        return base.ClassifyProviderRequestFailure(errorCode, errorType, errorMessage, responseBody);
    }

    /// <inheritdoc />
    protected override string GetProviderRequestFailureUserMessage(ProviderRequestFailureReason failureReason)
    {
        if (failureReason is not ProviderRequestFailureReason.MODEL_NOT_SUPPORTED_BY_PROVIDER)
            return base.GetProviderRequestFailureUserMessage(failureReason);

        //
        // When Hugging Face chose the provider itself, naming it back to the user would help
        // nobody: they never picked it, and no other choice of provider is left to try:
        //
        if (this.hfProvider is HFInferenceProvider.NONE or HFInferenceProvider.AUTOMATIC or HFInferenceProvider.CHEAPEST or HFInferenceProvider.PREFERRED)
            return TB("No Hugging Face inference provider offers the selected model. Please check the model name and whether it is still available on Hugging Face.");

        return string.Format(TB("The Hugging Face inference provider '{0}' does not offer the selected model. Please select another inference provider, or let Hugging Face choose one for you."), this.hfProvider.ToName());
    }

    #endregion

    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.HUGGINGFACE.ToSecretId();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "HuggingFace";

    /// <inheritdoc />
    public override bool HasModelLoadingCapability => true;

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
    public override async Task<TranscriptionResult> TranscribeAudioAsync(Model transcriptionModel, string audioFilePath, SettingsManager settingsManager, CancellationToken token = default)
    {
        var requestedSecret = await Program.RUST_SERVICE.GetAPIKey(this, SecretStoreType.TRANSCRIPTION_PROVIDER);

        //
        // Note that we send the model as it is: this request goes to the provider's own route,
        // where a routing suffix would be part of the name and name nothing:
        //
        return await this.PerformStandardTranscriptionRequest(requestedSecret, transcriptionModel, audioFilePath, token: token);
    }
    
    /// <inhertidoc />
    public override async Task<IReadOnlyList<IReadOnlyList<float>>> EmbedTextAsync(Model embeddingModel, SettingsManager settingsManager, CancellationToken token = default, params List<string> texts)
    {
        var requestedSecret = await Program.RUST_SERVICE.GetAPIKey(this, SecretStoreType.EMBEDDING_PROVIDER);

        //
        // Note that we send the model as it is: this request goes to the provider's own route,
        // where a routing suffix would be part of the name and name nothing:
        //
        return await this.PerformStandardTextEmbeddingRequest(requestedSecret, embeddingModel, token: token, texts: texts);
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModelsResponse<ModelsResponse>(SecretStoreType.LLM_PROVIDER, "models", this.SelectChatModels, token, apiKeyProvisional);
    }

    /// <summary>
    /// Picks the models the user may chat with through the chosen inference provider.
    /// </summary>
    /// <remarks>
    /// The router reports every model it knows, together with the providers serving it. When the
    /// user named a provider, we show what that provider offers and nothing else. Showing more
    /// would be a disservice: every model outside that list is answered with a bad request, and the
    /// user would only learn about it once they try to chat.
    /// </remarks>
    /// <param name="response">The response of the model endpoint.</param>
    /// <returns>The models to offer.</returns>
    private IEnumerable<Model> SelectChatModels(ModelsResponse response)
    {
        var chatModels = response.Data.Where(hfModel => new Model(hfModel.Id, null).IsChatModel());
        var providerSlug = this.hfProvider.EndpointsId();
        if (string.IsNullOrEmpty(providerSlug))
            return ToModels(chatModels);

        return ToModels(chatModels.Where(hfModel => IsServedBy(hfModel, providerSlug)));
    }

    private static IEnumerable<Model> ToModels(IEnumerable<HFModel> hfModels) => hfModels.Select(hfModel => new Model(hfModel.Id, null));

    private static bool IsServedBy(HFModel hfModel, string providerSlug)
    {
        if (hfModel.Providers is null)
            return false;

        return hfModel.Providers.Any(provider =>
            string.Equals(provider.Provider, providerSlug, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(provider.Status, "live", StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(ModelLoadResult.FromModels([]));
    }
    
    /// <inheritdoc />
    public override Task<ModelLoadResult> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        if (!this.hfProvider.SupportsEmbeddings())
            return Task.FromResult(ModelLoadResult.FromModels([]));

        return this.LoadHubModels(SecretStoreType.EMBEDDING_PROVIDER, "feature-extraction", apiKeyProvisional, token);
    }

    /// <summary>
    /// Loads the models one inference provider offers for a task, as the hub lists them.
    /// </summary>
    /// <param name="storeType">Which stored API key to use.</param>
    /// <param name="pipelineTag">The task to ask for, as the hub names it.</param>
    /// <param name="apiKeyProvisional">An API key which is not stored yet.</param>
    /// <param name="token">The cancellation token to use.</param>
    /// <returns>The models of that provider for that task.</returns>
    private Task<ModelLoadResult> LoadHubModels(SecretStoreType storeType, string pipelineTag, string? apiKeyProvisional, CancellationToken token)
    {
        var requestURL = $"{HUB_MODELS_URL}{this.hfProvider.EndpointsId()}&pipeline_tag={pipelineTag}";
        return this.LoadModelsResponse<IList<HubModel>>(storeType, requestURL, hubModels => hubModels.Select(hubModel => new Model(hubModel.Id, null)), token, apiKeyProvisional);
    }
    
    /// <inheritdoc />
    public override Task<ModelLoadResult> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        if (!this.hfProvider.SupportsTranscription())
            return Task.FromResult(ModelLoadResult.FromModels([]));

        return this.LoadHubModels(SecretStoreType.TRANSCRIPTION_PROVIDER, "automatic-speech-recognition", apiKeyProvisional, token);
    }
    
    #endregion
}