using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.Anthropic;

public sealed class ProviderAnthropic(ILogger logger) : BaseProvider("https://api.anthropic.com/v1/", logger)
{
    #region Implementation of IProvider

    public override string Id => LLMProviders.ANTHROPIC.ToName();

    public override string InstanceName { get; set; } = "Anthropic";

    /// <inheritdoc />
    public override async IAsyncEnumerable<string> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        // Get the API key:
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this);
        if(!requestedSecret.Success)
            yield break;

        // Prepare the Anthropic HTTP chat request:
        var chatRequest = JsonSerializer.Serialize(new ChatRequest
        {
            Model = chatModel.Id,
            
            // Build the messages:
            Messages = [..chatThread.Blocks.Where(n => n.ContentType is ContentType.TEXT && !string.IsNullOrWhiteSpace((n.Content as ContentText)?.Text)).Select(n => new Message
            {
                Role = n.Role switch
                {
                    ChatRole.USER => "user",
                    ChatRole.AI => "assistant",
                    ChatRole.AGENT => "assistant",

                    _ => "user",
                },

                Content = n.Content switch
                {
                    ContentText text => text.Text,
                    _ => string.Empty,
                }
            }).ToList()],
            
            System = chatThread.PrepareSystemPrompt(settingsManager, chatThread, this.logger),
            MaxTokens = 4_096,
            
            // Right now, we only support streaming completions:
            Stream = true,
        }, JSON_SERIALIZER_OPTIONS);

        async Task<HttpRequestMessage> RequestBuilder()
        {
            // Build the HTTP post request:
            var request = new HttpRequestMessage(HttpMethod.Post, "messages");

            // Set the authorization header:
            request.Headers.Add("x-api-key", await requestedSecret.Secret.Decrypt(ENCRYPTION));

            // Set the Anthropic version:
            request.Headers.Add("anthropic-version", "2023-06-01");

            // Set the content:
            request.Content = new StringContent(chatRequest, Encoding.UTF8, "application/json");
            return request;
        }
        
        await foreach (var content in this.StreamChatCompletionInternal<ResponseStreamLine>("Anthropic", RequestBuilder, token))
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
        var additionalModels = new[]
        {
            new Model("claude-opus-4-0", "Claude Opus 4.0 (Latest)"),
            new Model("claude-sonnet-4-0", "Claude Sonnet 4.0 (Latest)"),
            new Model("claude-3-7-sonnet-latest", "Claude 3.7 Sonnet (Latest)"),
            new Model("claude-3-5-sonnet-latest", "Claude 3.5 Sonnet (Latest)"),
            new Model("claude-3-5-haiku-latest", "Claude 3.5 Haiku (Latest)"),
            new Model("claude-3-opus-latest", "Claude 3 Opus (Latest)"),
        };
        
        return this.LoadModels(token, apiKeyProvisional).ContinueWith(t => t.Result.Concat(additionalModels).OrderBy(x => x.Id).AsEnumerable(), token);
    }

    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(Enumerable.Empty<Model>());
    }
    
    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(Enumerable.Empty<Model>());
    }

    public override IReadOnlyCollection<Capability> GetModelCapabilities(Model model)
    {
        var modelName = model.Id.ToLowerInvariant().AsSpan();
        
        // Claude 4.x models:
        if(modelName.StartsWith("claude-opus-4") || modelName.StartsWith("claude-sonnet-4"))
            return [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,
                
                Capability.OPTIONAL_REASONING, Capability.FUNCTION_CALLING];
        
        // Claude 3.7 is able to do reasoning:
        if(modelName.StartsWith("claude-3-7"))
            return [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,
                
                Capability.OPTIONAL_REASONING, Capability.FUNCTION_CALLING];
        
        // All other 3.x models are able to process text and images as input:
        if(modelName.StartsWith("claude-3-"))
            return [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,
                
                Capability.FUNCTION_CALLING];
        
        // Any other model is able to process text only:
        return [
            Capability.TEXT_INPUT,
            Capability.TEXT_OUTPUT,
            Capability.FUNCTION_CALLING];
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
        
        using var request = new HttpRequestMessage(HttpMethod.Get, "models?limit=100");
        
        // Set the authorization header:
        request.Headers.Add("x-api-key", secretKey);

        // Set the Anthropic version:
        request.Headers.Add("anthropic-version", "2023-06-01");
        
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);

        using var response = await this.httpClient.SendAsync(request, token);
        if(!response.IsSuccessStatusCode)
            return [];

        var modelResponse = await response.Content.ReadFromJsonAsync<ModelsResponse>(JSON_SERIALIZER_OPTIONS, token);
        return modelResponse.Data;
    }
}