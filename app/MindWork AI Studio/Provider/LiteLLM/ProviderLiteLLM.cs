using System.Runtime.CompilerServices;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.LiteLLM;

public sealed class ProviderLiteLLM(string hostname) : BaseProvider(LLMProviders.LITE_LLM, BuildBaseUri(hostname), ExternalHttpTrustPolicy.ALLOW_CUSTOM_ROOTS_WHEN_HOST_WHITELISTED, LOGGER)
{
    private static readonly ILogger<ProviderLiteLLM> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderLiteLLM>();

    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.LITE_LLM.ToSecretId();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "LiteLLM";

    /// <inheritdoc />
    public override bool HasModelLoadingCapability => true;

    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        await foreach (var content in this.StreamOpenAICompatibleChatCompletion<ChatCompletionAPIRequest, ChatCompletionDeltaStreamLine, NoChatCompletionAnnotationStreamLine>(
                           "LiteLLM",
                           chatModel,
                           chatThread,
                           settingsManager,
                           async (systemPrompt, apiParameters, tools) =>
                           {
                               // Build the list of messages:
                               var messages = await chatThread.Blocks.BuildMessagesUsingDirectImageUrlAsync(this.Provider, chatModel);

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

    /// <inheritdoc />
    public override async Task<IReadOnlyList<IReadOnlyList<float>>> EmbedTextAsync(Model embeddingModel, SettingsManager settingsManager, CancellationToken token = default, params List<string> texts)
    {
        var requestedSecret = await Program.RUST_SERVICE.GetAPIKey(this, SecretStoreType.EMBEDDING_PROVIDER);
        return await this.PerformStandardTextEmbeddingRequest(requestedSecret, embeddingModel, token: token, texts: texts);
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModels(SecretStoreType.LLM_PROVIDER, static model => model.IsChatModel(), token, apiKeyProvisional);
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(ModelLoadResult.FromModels([]));
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModels(SecretStoreType.EMBEDDING_PROVIDER, static model => model.IsEmbeddingModel(), token, apiKeyProvisional);
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModels(SecretStoreType.TRANSCRIPTION_PROVIDER, static model => model.IsTranscriptionModel(), token, apiKeyProvisional);
    }

    #endregion

    private static Uri BuildBaseUri(string hostname)
    {
        // LiteLLM exposes an OpenAI-compatible API under the "/v1/" path. Users configure the
        // base URL of their LiteLLM proxy (e.g. http://localhost:4000); we normalize any trailing
        // slash and append the OpenAI-compatible path.
        var normalizedHostname = hostname.TrimEnd('/');
        return new Uri($"{normalizedHostname}/v1/");
    }

    private Task<ModelLoadResult> LoadModels(SecretStoreType storeType, Func<Model, bool> isWantedKind, CancellationToken token, string? apiKeyProvisional = null)
    {
        //
        // The gateway serves every kind of model through one endpoint, so we have to sort
        // them apart ourselves. We use the shared model kind detection for that, which every
        // other provider uses as well:
        //
        return this.LoadModelsResponse<ModelsResponse>(
            storeType,
            "models",
            modelResponse => modelResponse.Data.Where(IsRealModel).Where(isWantedKind),
            token,
            apiKeyProvisional);
    }

    /// <summary>
    /// Checks whether this entry is a model at all, or one of LiteLLM's wildcards.
    /// </summary>
    /// <remarks>
    /// A LiteLLM configuration may pass a whole provider through at once, written as "openai/*" or
    /// just "*". Those patterns show up among the models, but they are no models: asking the gateway
    /// for one of them fails. No model carries an asterisk in its name, which makes it a safe mark.
    /// </remarks>
    /// <param name="model">The entry to check.</param>
    /// <returns>True, when the entry is a model rather than a wildcard.</returns>
    private static bool IsRealModel(Model model) => !model.Id.Contains('*');
}