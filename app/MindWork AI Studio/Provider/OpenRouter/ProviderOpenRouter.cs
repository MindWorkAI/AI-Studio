using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.OpenRouter;

public sealed class ProviderOpenRouter() : BaseProvider(LLMProviders.OPEN_ROUTER, "https://openrouter.ai/api/v1/", LOGGER)
{
    private const string PROJECT_WEBSITE = "https://github.com/MindWorkAI/AI-Studio";
    private const string PROJECT_NAME = "MindWork AI Studio";

    private static readonly ILogger<ProviderOpenRouter> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderOpenRouter>();

    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.OPEN_ROUTER.ToName();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "OpenRouter";

    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        // Get the API key:
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this);
        if(!requestedSecret.Success)
            yield break;

        // Prepare the system prompt:
        var systemPrompt = new TextMessage
        {
            Role = "system",
            Content = chatThread.PrepareSystemPrompt(settingsManager, chatThread),
        };

        // Parse the API parameters:
        var apiParameters = this.ParseAdditionalApiParameters();
        
        // Build the list of messages:
        var messages = await chatThread.Blocks.BuildMessagesUsingStandardRoles();

        // Prepare the OpenRouter HTTP chat request:
        var openRouterChatRequest = JsonSerializer.Serialize(new ChatCompletionAPIRequest
        {
            Model = chatModel.Id,

            // Build the messages:
            // - First of all the system prompt
            // - Then none-empty user and AI messages
            Messages = [systemPrompt, ..messages],

            // Right now, we only support streaming completions:
            Stream = true,
            AdditionalApiParameters = apiParameters
        }, JSON_SERIALIZER_OPTIONS);

        async Task<HttpRequestMessage> RequestBuilder()
        {
            // Build the HTTP post request:
            var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");

            // Set the authorization header:
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await requestedSecret.Secret.Decrypt(ENCRYPTION));
            
            // Set custom headers for project identification:
            request.Headers.Add("HTTP-Referer", PROJECT_WEBSITE);
            request.Headers.Add("X-Title", PROJECT_NAME);

            // Set the content:
            request.Content = new StringContent(openRouterChatRequest, Encoding.UTF8, "application/json");
            return request;
        }

        await foreach (var content in this.StreamChatCompletionInternal<ChatCompletionDeltaStreamLine, NoChatCompletionAnnotationStreamLine>("OpenRouter", RequestBuilder, token))
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
    public override Task<IEnumerable<Model>> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModels(token, apiKeyProvisional);
    }

    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(Enumerable.Empty<Model>());
    }

    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadEmbeddingModels(token, apiKeyProvisional);
    }

    #endregion

    private async Task<IEnumerable<Model>> LoadModels(CancellationToken token, string? apiKeyProvisional = null)
    {
        var secretKey = apiKeyProvisional switch
        {
            not null => apiKeyProvisional,
            _ => await RUST_SERVICE.GetAPIKey(this) switch
            {
                { Success: true } result => await result.Secret.Decrypt(ENCRYPTION),
                _ => null,
            }
        };

        if (secretKey is null)
            return [];

        using var request = new HttpRequestMessage(HttpMethod.Get, "models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
        
        // Set custom headers for project identification:
        request.Headers.Add("HTTP-Referer", PROJECT_WEBSITE);
        request.Headers.Add("X-Title", PROJECT_NAME);

        using var response = await this.httpClient.SendAsync(request, token);
        if(!response.IsSuccessStatusCode)
            return [];

        var modelResponse = await response.Content.ReadFromJsonAsync<OpenRouterModelsResponse>(token);

        // Filter out non-text models (image, audio, embedding models) and convert to Model
        return modelResponse.Data
            .Where(n =>
                !n.Id.Contains("whisper", StringComparison.OrdinalIgnoreCase) &&
                !n.Id.Contains("dall-e", StringComparison.OrdinalIgnoreCase) &&
                !n.Id.Contains("tts", StringComparison.OrdinalIgnoreCase) &&
                !n.Id.Contains("embedding", StringComparison.OrdinalIgnoreCase) &&
                !n.Id.Contains("moderation", StringComparison.OrdinalIgnoreCase) &&
                !n.Id.Contains("stable-diffusion", StringComparison.OrdinalIgnoreCase) &&
                !n.Id.Contains("flux", StringComparison.OrdinalIgnoreCase) &&
                !n.Id.Contains("midjourney", StringComparison.OrdinalIgnoreCase))
            .Select(n => new Model(n.Id, n.Name));
    }

    private async Task<IEnumerable<Model>> LoadEmbeddingModels(CancellationToken token, string? apiKeyProvisional = null)
    {
        var secretKey = apiKeyProvisional switch
        {
            not null => apiKeyProvisional,
            _ => await RUST_SERVICE.GetAPIKey(this) switch
            {
                { Success: true } result => await result.Secret.Decrypt(ENCRYPTION),
                _ => null,
            }
        };

        if (secretKey is null)
            return [];

        using var request = new HttpRequestMessage(HttpMethod.Get, "embeddings/models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
        
        // Set custom headers for project identification:
        request.Headers.Add("HTTP-Referer", PROJECT_WEBSITE);
        request.Headers.Add("X-Title", PROJECT_NAME);

        using var response = await this.httpClient.SendAsync(request, token);
        if(!response.IsSuccessStatusCode)
            return [];

        var modelResponse = await response.Content.ReadFromJsonAsync<OpenRouterModelsResponse>(token);

        // Convert all embedding models to Model
        return modelResponse.Data.Select(n => new Model(n.Id, n.Name));
    }
}
