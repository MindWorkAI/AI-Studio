using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.Mistral;

public sealed class ProviderMistral(ILogger logger) : BaseProvider("https://api.mistral.ai/v1/", logger)
{
    #region Implementation of IProvider

    public override string Id => LLMProviders.MISTRAL.ToName();
    
    public override string InstanceName { get; set; } = "Mistral";

    /// <inheritdoc />
    public override async IAsyncEnumerable<string> StreamChatCompletion(Provider.Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        // Get the API key:
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this);
        if(!requestedSecret.Success)
            yield break;

        // Prepare the system prompt:
        var systemPrompt = new RegularMessage
        {
            Role = "system",
            Content = chatThread.PrepareSystemPrompt(settingsManager, chatThread, this.logger),
        };
        
        // Prepare the Mistral HTTP chat request:
        var mistralChatRequest = JsonSerializer.Serialize(new ChatRequest
        {
            Model = chatModel.Id,
            
            // Build the messages:
            // - First of all the system prompt
            // - Then none-empty user and AI messages
            Messages = [systemPrompt, ..chatThread.Blocks.Where(n => n.ContentType is ContentType.TEXT && !string.IsNullOrWhiteSpace((n.Content as ContentText)?.Text)).Select(n => new RegularMessage
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
                    ContentText text => text.Text,
                    _ => string.Empty,
                }
            }).ToList()],

            RandomSeed = chatThread.Seed,
            
            // Right now, we only support streaming completions:
            Stream = true,
            SafePrompt = false,
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
        
        await foreach (var content in this.StreamChatCompletionInternal<ResponseStreamLine>("Mistral", RequestBuilder, token))
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
    
    public override IReadOnlyCollection<Capability> GetModelCapabilities(Provider.Model model)
    {
        var modelName = model.Id.ToLowerInvariant().AsSpan();
        
        // Pixtral models are able to do process images:
        if (modelName.IndexOf("pixtral") is not -1)
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,
                
                Capability.FUNCTION_CALLING,
            ];
        
        // Mistral medium:
        if (modelName.IndexOf("mistral-medium-") is not -1)
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,
                
                Capability.FUNCTION_CALLING,
            ];
        
        // Mistral small:
        if (modelName.IndexOf("mistral-small-") is not -1)
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,
                
                Capability.FUNCTION_CALLING,
            ];
        
        // Mistral saba:
        if (modelName.IndexOf("mistral-saba-") is not -1)
            return
            [
                Capability.TEXT_INPUT,
                Capability.TEXT_OUTPUT,
            ];
        
        // Default:
        return CapabilitiesOpenSource.GetCapabilities(model);
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