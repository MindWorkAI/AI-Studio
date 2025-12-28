using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.Mistral;

public sealed class ProviderMistral() : BaseProvider(LLMProviders.MISTRAL, "https://api.mistral.ai/v1/", LOGGER)
{
    private static readonly ILogger<ProviderMistral> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderMistral>();

    #region Implementation of IProvider

    public override string Id => LLMProviders.MISTRAL.ToName();
    
    public override string InstanceName { get; set; } = "Mistral";

    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Provider.Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
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
        var messages = await chatThread.Blocks.BuildMessages(async n => new TextMessage
        {
            Role = n.Role switch
            {
                ChatRole.USER => "user",
                ChatRole.AI => "assistant",
                ChatRole.AGENT => "assistant",
                ChatRole.SYSTEM => "system",

                _ => "user",
            },

            Content = n.Content switch
            {
                ContentText text => await text.PrepareTextContentForAI(),
                _ => string.Empty,
            }
        });
        
        // Prepare the Mistral HTTP chat request:
        var mistralChatRequest = JsonSerializer.Serialize(new ChatRequest
        {
            Model = chatModel.Id,
            
            // Build the messages:
            // - First of all the system prompt
            // - Then none-empty user and AI messages
            Messages = [systemPrompt, ..messages],
            
            // Right now, we only support streaming completions:
            Stream = true,
            SafePrompt = apiParameters.TryGetValue("safe_prompt", out var value) && value is true,
            AdditionalApiParameters = apiParameters
        }, JSON_SERIALIZER_OPTIONS);

        
        async Task<HttpRequestMessage> RequestBuilder()
        {
            // Build the HTTP post request:
            var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");

            // Set the authorization header:
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await requestedSecret.Secret.Decrypt(ENCRYPTION));

            // Set the content:
            request.Content = new StringContent(mistralChatRequest, Encoding.UTF8, "application/json");
            return request;
        }
        
        await foreach (var content in this.StreamChatCompletionInternal<ChatCompletionDeltaStreamLine, NoChatCompletionAnnotationStreamLine>("Mistral", RequestBuilder, token))
            yield return content;
    }

    #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    /// <inheritdoc />
    public override async IAsyncEnumerable<ImageURL> StreamImageCompletion(Provider.Model imageModel, string promptPositive, string promptNegative = FilterOperator.String.Empty, ImageURL referenceImageURL = default, [EnumeratorCancellation] CancellationToken token = default)
    {
        yield break;
    }
    #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

    /// <inheritdoc />
    public override async Task<IEnumerable<Provider.Model>> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var modelResponse = await this.LoadModelList(apiKeyProvisional, token);
        if(modelResponse == default)
            return [];
        
        return modelResponse.Data.Where(n => 
            !n.Id.StartsWith("code", StringComparison.OrdinalIgnoreCase) &&
            !n.Id.Contains("embed", StringComparison.OrdinalIgnoreCase) &&
            !n.Id.Contains("moderation", StringComparison.OrdinalIgnoreCase))
            .Select(n => new Provider.Model(n.Id, null));
    }
    
    /// <inheritdoc />
    public override async Task<IEnumerable<Provider.Model>> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var modelResponse = await this.LoadModelList(apiKeyProvisional, token);
        if(modelResponse == default)
            return [];
        
        return modelResponse.Data.Where(n => n.Id.Contains("embed", StringComparison.InvariantCulture))
            .Select(n => new Provider.Model(n.Id, null));
    }
    
    /// <inheritdoc />
    public override Task<IEnumerable<Provider.Model>> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(Enumerable.Empty<Provider.Model>());
    }
    
    #endregion
    
    private async Task<ModelsResponse> LoadModelList(string? apiKeyProvisional, CancellationToken token)
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
            return default;
        
        using var request = new HttpRequestMessage(HttpMethod.Get, "models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);

        using var response = await this.httpClient.SendAsync(request, token);
        if(!response.IsSuccessStatusCode)
            return default;

        var modelResponse = await response.Content.ReadFromJsonAsync<ModelsResponse>(token);
        return modelResponse;
    }
}