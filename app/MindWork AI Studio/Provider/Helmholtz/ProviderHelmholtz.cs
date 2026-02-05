using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
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
        // Get the API key:
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this, SecretStoreType.LLM_PROVIDER);
        if(!requestedSecret.Success)
            yield break;
        
        // Prepare the system prompt:
        var systemPrompt = new TextMessage
        {
            Role = "system",
            Content = chatThread.PrepareSystemPrompt(settingsManager),
        };
        
        // Parse the API parameters:
        var apiParameters = this.ParseAdditionalApiParameters();
            
        // Build the list of messages:
        var messages = await chatThread.Blocks.BuildMessagesUsingNestedImageUrlAsync(this.Provider, chatModel);
        
        // Prepare the Helmholtz HTTP chat request:
        var helmholtzChatRequest = JsonSerializer.Serialize(new ChatCompletionAPIRequest
        {
            Model = chatModel.Id,
            
            // Build the messages:
            // - First of all the system prompt
            // - Then none-empty user and AI messages
            Messages = [systemPrompt, ..messages],
                
            Stream = true,
            AdditionalApiParameters = apiParameters
        }, JSON_SERIALIZER_OPTIONS);

        async Task<HttpRequestMessage> RequestBuilder()
        {
            // Build the HTTP post request:
            var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");

            // Set the authorization header:
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await requestedSecret.Secret.Decrypt(ENCRYPTION));

            // Set the content:
            request.Content = new StringContent(helmholtzChatRequest, Encoding.UTF8, "application/json");
            return request;
        }
        
        await foreach (var content in this.StreamChatCompletionInternal<ChatCompletionDeltaStreamLine, ChatCompletionAnnotationStreamLine>("Helmholtz", RequestBuilder, token))
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
    public override async Task<IReadOnlyList<IReadOnlyList<float>>> EmbedTextAsync(Provider.Model embeddingModel, SettingsManager settingsManager, CancellationToken token = default, params List<string> texts)
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
        if(!response.IsSuccessStatusCode)
            return [];

        var modelResponse = await response.Content.ReadFromJsonAsync<ModelsResponse>(token);
        return modelResponse.Data;
    }
}