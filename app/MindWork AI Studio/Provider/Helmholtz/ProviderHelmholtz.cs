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
    public override async Task<IEnumerable<Model>> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var models = await this.LoadModels(SecretStoreType.LLM_PROVIDER, token, apiKeyProvisional);
        return models.Where(model => !model.Id.StartsWith("text-", StringComparison.InvariantCultureIgnoreCase) &&
                                     !model.Id.StartsWith("alias-embedding", StringComparison.InvariantCultureIgnoreCase));
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
        return models.Where(model => 
            model.Id.StartsWith("alias-embedding", StringComparison.InvariantCultureIgnoreCase) ||
            model.Id.StartsWith("text-", StringComparison.InvariantCultureIgnoreCase) ||
            model.Id.Contains("gritlm", StringComparison.InvariantCultureIgnoreCase));
    }
    
    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(Enumerable.Empty<Model>());
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
        
        // Unfortunately, the Helmholtz API does not return a non-success status code when the API key is invalid. Instead, it returns a 200 OK with a body that contains an error message.
        // Therefore, we have to check the body of the response to determine if the request was successful or not.
        if(!response.IsSuccessStatusCode)
            return [];

        try
        {
            var modelResponse = await response.Content.ReadFromJsonAsync<ModelsResponse>(token);
            return modelResponse.Data;
        }
        catch (JsonException e)
        {
            //
            // We expect a JsonException to be thrown when the API key is invalid, because the body of the response will not
            // be a valid JSON. Therefore, we catch this exception and show an appropriate error message to the user.
            //
            var body = await response.Content.ReadAsStringAsync(token);
            
            if(body.Contains("invalid API key", StringComparison.InvariantCultureIgnoreCase) ||
               body.Contains("missing API key", StringComparison.InvariantCultureIgnoreCase))
            {
                LOGGER.LogWarning("Invalid API key provided for provider {ProviderId}. The response body was: '{ResponseBody}'", this.Id, body);
                return [];
            }
            
            LOGGER.LogError(e, "Unexpected error while parsing models from Helmholtz API response. Status Code: {StatusCode}. Reason: {ReasonPhrase}. Response Body: '{ResponseBody}'", response.StatusCode, response.ReasonPhrase, body);
            return [];
        }
        catch (Exception e)
        {
            LOGGER.LogError(e, "Unexpected error while loading models from Helmholtz API. Status Code: {StatusCode}. Reason: {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
            return [];
        }
    }
}