using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.Helmholtz;

public sealed class ProviderHelmholtz() : BaseProvider(LLMProviders.HELMHOLTZ, "https://api.helmholtz-blablador.fz-juelich.de/v1/", LOGGER)
{
    private static readonly ILogger<ProviderHelmholtz> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderHelmholtz>();

    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.HELMHOLTZ.ToName();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "Helmholtz Blablador";

    /// <inheritdoc />
    public override bool HasModelLoadingCapability => true;
    
    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        await foreach (var content in this.StreamOpenAICompatibleChatCompletion<ChatCompletionAPIRequest, ChatCompletionDeltaStreamLine, ChatCompletionAnnotationStreamLine>(
                           "Helmholtz",
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
        var result = await this.LoadModels(SecretStoreType.LLM_PROVIDER, token, apiKeyProvisional);
        return result with
        {
            Models =
            [
                ..result.Models.Where(model => !model.Id.StartsWith("text-", StringComparison.InvariantCultureIgnoreCase) &&
                                               !model.Id.Contains("-embedding", StringComparison.InvariantCultureIgnoreCase)
                                               )
            ]
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
        var result = await this.LoadModels(SecretStoreType.EMBEDDING_PROVIDER, token, apiKeyProvisional);
        return result with
        {
            Models =
            [
                ..result.Models.Where(model =>
                    model.Id.Contains("-embedding", StringComparison.InvariantCultureIgnoreCase) ||
                    model.Id.StartsWith("text-", StringComparison.InvariantCultureIgnoreCase) ||
                    model.Id.Contains("gritlm", StringComparison.InvariantCultureIgnoreCase))
            ]
        };
    }
    
    /// <inheritdoc />
    public override Task<ModelLoadResult> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(ModelLoadResult.FromModels([]));
    }
    
    #endregion

    private async Task<ModelLoadResult> LoadModels(SecretStoreType storeType, CancellationToken token, string? apiKeyProvisional = null)
    {
        var secretKey = await this.GetModelLoadingSecretKey(storeType, apiKeyProvisional);
        if (string.IsNullOrWhiteSpace(secretKey))
            return FailedModelLoadResult(ModelLoadFailureReason.INVALID_OR_MISSING_API_KEY, "No API key available for model loading.");

        using var request = new HttpRequestMessage(HttpMethod.Get, "models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);

        using var response = await this.HttpClient.SendAsync(request, token);
        var body = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode)
            return FailedModelLoadResult(GetDefaultModelLoadFailureReason(response), $"Status={(int)response.StatusCode} {response.ReasonPhrase}; Body='{body}'");

        try
        {
            var modelResponse = JsonSerializer.Deserialize<ModelsResponse>(body, JSON_SERIALIZER_OPTIONS);
            return SuccessfulModelLoadResult(modelResponse.Data);
        }
        catch (JsonException e)
        {
            if (body.Contains("API key", StringComparison.InvariantCultureIgnoreCase))
                return FailedModelLoadResult(ModelLoadFailureReason.INVALID_OR_MISSING_API_KEY, body);
            
            LOGGER.LogError(e, "Unexpected error while parsing models from Helmholtz API response. Status Code: {StatusCode}. Reason: {ReasonPhrase}. Response Body: '{ResponseBody}'", response.StatusCode, response.ReasonPhrase, body);
            return FailedModelLoadResult(ModelLoadFailureReason.INVALID_RESPONSE, body);
        }
        catch (Exception e)
        {
            LOGGER.LogError(e, "Unexpected error while loading models from Helmholtz API. Status Code: {StatusCode}. Reason: {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
            return FailedModelLoadResult(ModelLoadFailureReason.UNKNOWN, e.Message);
        }
    }
}