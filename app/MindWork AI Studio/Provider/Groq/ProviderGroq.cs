using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.Groq;

public class ProviderGroq(ILogger logger) : BaseProvider("https://api.groq.com/openai/v1/", logger)
{
    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.GROQ.ToName();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "Groq";

    /// <inheritdoc />
    public override async IAsyncEnumerable<string> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        // Get the API key:
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this);
        if(!requestedSecret.Success)
            yield break;

        // Prepare the system prompt:
        var systemPrompt = new Message
        {
            Role = "system",
            Content = chatThread.PrepareSystemPrompt(settingsManager, chatThread, this.logger),
        };
        
        // Prepare the OpenAI HTTP chat request:
        var groqChatRequest = JsonSerializer.Serialize(new ChatRequest
        {
            Model = chatModel.Id,
            
            // Build the messages:
            // - First of all the system prompt
            // - Then none-empty user and AI messages
            Messages = [systemPrompt, ..chatThread.Blocks.Where(n => n.ContentType is ContentType.TEXT && !string.IsNullOrWhiteSpace((n.Content as ContentText)?.Text)).Select(n => new Message
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

            Seed = chatThread.Seed,
            
            // Right now, we only support streaming completions:
            Stream = true,
        }, JSON_SERIALIZER_OPTIONS);

        async Task<HttpRequestMessage> RequestBuilder()
        {
            // Build the HTTP post request:
            var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");

            // Set the authorization header:
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await requestedSecret.Secret.Decrypt(ENCRYPTION));

            // Set the content:
            request.Content = new StringContent(groqChatRequest, Encoding.UTF8, "application/json");
            return request;
        }
        
        await foreach (var content in this.StreamChatCompletionInternal<ResponseStreamLine>("Groq", RequestBuilder, token))
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
        return Task.FromResult<IEnumerable<Model>>(Array.Empty<Model>());
    }
    
    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(Enumerable.Empty<Model>());
    }
    
    public override IReadOnlyCollection<Capability> GetModelCapabilities(Model model) => CapabilitiesOpenSource.GetCapabilities(model);

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

        using var response = await this.httpClient.SendAsync(request, token);
        if(!response.IsSuccessStatusCode)
            return [];

        var modelResponse = await response.Content.ReadFromJsonAsync<ModelsResponse>(token);
        return modelResponse.Data.Where(n =>
            !n.Id.StartsWith("whisper-", StringComparison.OrdinalIgnoreCase) &&
            !n.Id.StartsWith("distil-", StringComparison.OrdinalIgnoreCase) &&
            !n.Id.Contains("-tts", StringComparison.OrdinalIgnoreCase));
    }
}