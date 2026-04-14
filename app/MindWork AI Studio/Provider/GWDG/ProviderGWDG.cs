using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.GWDG;

public sealed class ProviderGWDG() : BaseProvider(LLMProviders.GWDG, "https://chat-ai.academiccloud.de/v1/", LOGGER)
{
    private static readonly ILogger<ProviderGWDG> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderGWDG>();

    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.GWDG.ToName();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "GWDG SAIA";
    
    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        await foreach (var content in this.StreamOpenAICompatibleChatCompletion<ChatCompletionAPIRequest, ChatCompletionDeltaStreamLine, ChatCompletionAnnotationStreamLine>(
                           "GWDG",
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
    public override async Task<string> TranscribeAudioAsync(Model transcriptionModel, string audioFilePath, SettingsManager settingsManager, CancellationToken token = default)
    {
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this, SecretStoreType.TRANSCRIPTION_PROVIDER);
        return await this.PerformStandardTranscriptionRequest(requestedSecret, transcriptionModel, audioFilePath, token: token);
    }
    
    /// <inhertidoc />
    public override Task<IReadOnlyList<IReadOnlyList<float>>> EmbedTextAsync(Model embeddingModel, SettingsManager settingsManager, CancellationToken token = default, params List<string> texts)
    {
        return Task.FromResult<IReadOnlyList<IReadOnlyList<float>>>([]);
    }

    /// <inheritdoc />
    public override async Task<IEnumerable<Model>> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var models = await this.LoadModels(SecretStoreType.LLM_PROVIDER, token, apiKeyProvisional);
        return models.Where(model => !model.Id.StartsWith("e5-mistral-7b-instruct", StringComparison.InvariantCultureIgnoreCase));
    }

    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(Enumerable.Empty<Model>());
    }
    
    /// <inheritdoc />
    public override async Task<IEnumerable<Model>> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var models = await this.LoadModels(SecretStoreType.EMBEDDING_PROVIDER, token, apiKeyProvisional);
        return models.Where(model => model.Id.StartsWith("e5-", StringComparison.InvariantCultureIgnoreCase));
    }
    
    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        // Source: https://docs.hpc.gwdg.de/services/saia/index.html#voice-to-text
        return Task.FromResult<IEnumerable<Model>>(
            new List<Model>
            {
                new("whisper-large-v2", "Whisper v2 Large"),
            });
    }
    
    #endregion

    private async Task<IEnumerable<Model>> LoadModels(SecretStoreType storeType, CancellationToken token, string? apiKeyProvisional = null)
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
            return [];
        
        using var request = new HttpRequestMessage(HttpMethod.Get, "models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);

        using var response = await this.httpClient.SendAsync(request, token);
        if(!response.IsSuccessStatusCode)
        {
            if(response.StatusCode is HttpStatusCode.Unauthorized)
                LOGGER.LogWarning("Unauthorized access when loading models for provider {ProviderId}. Please check the API key. Status Code: {StatusCode}. Reason: {ReasonPhrase}", this.Id, response.StatusCode, response.ReasonPhrase);
            else
                LOGGER.LogWarning("Failed to load models for provider {ProviderId}. Status Code: {StatusCode}. Reason: {ReasonPhrase}", this.Id, response.StatusCode, response.ReasonPhrase);
            
            return [];
        }

        try
        {
            var modelResponse = await response.Content.ReadFromJsonAsync<ModelsResponse>(token);
            return modelResponse.Data;
        }
        catch (Exception e)
        {
            LOGGER.LogError(e, "Exception occurred while loading models for provider {ProviderId}. Exception Message: {ExceptionMessage}", this.Id, e.Message);
            return [];
        }
    }
}