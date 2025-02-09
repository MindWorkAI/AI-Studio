using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Settings;

namespace AIStudio.Provider.OpenAI;

/// <summary>
/// The OpenAI provider.
/// </summary>
public sealed class ProviderOpenAI(ILogger logger) : BaseProvider("https://api.openai.com/v1/", logger)
{
    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.OPEN_AI.ToName();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "OpenAI";

    /// <inheritdoc />
    public override async IAsyncEnumerable<string> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        // Get the API key:
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this);
        if(!requestedSecret.Success)
            yield break;
        
        // Unfortunately, OpenAI changed the name of the system prompt based on the model.
        // All models that start with "o" (the omni aka reasoning models) and all GPT4o models
        // have the system prompt named "developer". All other models have the system prompt
        // named "system". We need to check this to get the correct system prompt.
        //
        // To complicate it even more: The early versions of reasoning models, which are released
        // before the 17th of December 2024, have no system prompt at all. We need to check this
        // as well.
        
        // Apply the basic rule first:
        var systemPromptRole = chatModel.Id.StartsWith('o') || chatModel.Id.Contains("4o") ? "developer" : "system";
        
        // Check if the model is an early version of the reasoning models:
        systemPromptRole = chatModel.Id switch
        {
            "o1-mini" => "user",
            "o1-mini-2024-09-12" => "user",
            "o1-preview" => "user",
            "o1-preview-2024-09-12" => "user",
            
            _ => systemPromptRole,
        };
        
        this.logger.LogInformation($"Using the system prompt role '{systemPromptRole}' for model '{chatModel.Id}'.");

        // Prepare the system prompt:
        var systemPrompt = new Message
        {
            Role = systemPromptRole,
            Content = chatThread.PrepareSystemPrompt(settingsManager, chatThread, this.logger),
        };
        
        // Prepare the OpenAI HTTP chat request:
        var openAIChatRequest = JsonSerializer.Serialize(new ChatRequest
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
                    ChatRole.SYSTEM => systemPromptRole,

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
            request.Content = new StringContent(openAIChatRequest, Encoding.UTF8, "application/json");
            return request;
        }
        
        await foreach (var content in this.StreamChatCompletionInternal<ResponseStreamLine>("OpenAI", RequestBuilder, token))
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
        return this.LoadModels(["gpt-", "o1-"], token, apiKeyProvisional);
    }

    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModels(["dall-e-"], token, apiKeyProvisional);
    }
    
    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModels(["text-embedding-"], token, apiKeyProvisional);
    }

    #endregion

    private async Task<IEnumerable<Model>> LoadModels(string[] prefixes, CancellationToken token, string? apiKeyProvisional = null)
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
        return modelResponse.Data.Where(model => prefixes.Any(prefix => model.Id.StartsWith(prefix, StringComparison.InvariantCulture)));
    }
}