using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

using AIStudio.Chat;
using AIStudio.Models.Live;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.Requesty;

public sealed class ProviderRequesty() : BaseProvider(LLMProviders.REQUESTY, new Uri("https://router.requesty.ai/v1/"), ExternalHttpTrustPolicy.SYSTEM_TRUST_ONLY, LOGGER)
{
    private const string PROJECT_WEBSITE = "https://github.com/MindWorkAI/AI-Studio";
    private const string PROJECT_NAME = "MindWork AI Studio";

    private static readonly ILogger<ProviderRequesty> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderRequesty>();

    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.REQUESTY.ToSecretId();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "Requesty";

    /// <inheritdoc />
    public override bool HasModelLoadingCapability => true;

    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        await foreach (var content in this.StreamOpenAICompatibleChatCompletion<ChatCompletionAPIRequest, ChatCompletionDeltaStreamLine, NoChatCompletionAnnotationStreamLine>(
                           "Requesty",
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

                                   // Right now, we only support streaming completions:
                                   Stream = true,
                                   Tools = tools,
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
    public override Task<TranscriptionResult> TranscribeAudioAsync(Model transcriptionModel, string audioFilePath, SettingsManager settingsManager, CancellationToken token = default)
    {
        return Task.FromResult(TranscriptionResult.Failure());
    }

    /// <inhertidoc />
    public override Task<IReadOnlyList<IReadOnlyList<float>>> EmbedTextAsync(Model embeddingModel, SettingsManager settingsManager, CancellationToken token = default, params List<string> texts)
    {
        throw this.CreateEmbeddingsNotSupportedException();
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModels(SecretStoreType.LLM_PROVIDER, apiKeyProvisional, token);
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

    /// <summary>
    /// Loads the managed policies and the full model catalog, and offers both as one list.
    /// </summary>
    /// <remarks>
    /// Requesty lists its managed policies, such as "gpt-5-mini@eu", on a route of their own, and
    /// the catalog does not contain them. Both lists are read before anything is reported, because
    /// what is reported replaces everything this instance said before. When the managed route
    /// fails, the catalog alone is still offered.
    /// </remarks>
    /// <param name="storeType">Where the API key is stored.</param>
    /// <param name="apiKeyProvisional">An API key which is not stored yet.</param>
    /// <param name="token">The cancellation token to use.</param>
    /// <returns>The chat models.</returns>
    private async Task<ModelLoadResult> LoadModels(SecretStoreType storeType, string? apiKeyProvisional, CancellationToken token)
    {
        IList<RequestyModel> managedModels = [];
        await this.LoadModelsResponse<RequestyModelsResponse>(
            storeType,
            "models/managed",
            modelResponse =>
            {
                managedModels = modelResponse.Data;
                return [];
            },
            apiKeyProvisional,
            requestConfigurator: ConfigureRequest,
            token: token);

        return await this.LoadModelsResponse<RequestyModelsResponse>(
            storeType,
            "models",
            modelResponse => managedModels.Concat(modelResponse.Data)
                .DistinctBy(n => n.Id)
                .Select(n => new Model(n.Id, null))
                .Where(model => model.IsChatModel(this.Provider)),
            apiKeyProvisional,
            requestConfigurator: ConfigureRequest,
            listingFactory: modelResponse => managedModels.Concat(modelResponse.Data)
                .DistinctBy(n => n.Id)
                .Select(n => ModelListing.For(n.Id, n.ContextWindowTokens)),
            token: token);
    }

    private static void ConfigureRequest(HttpRequestMessage request, string secretKey)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
        request.Headers.Add("HTTP-Referer", PROJECT_WEBSITE);
        request.Headers.Add("X-Title", PROJECT_NAME);
    }
}