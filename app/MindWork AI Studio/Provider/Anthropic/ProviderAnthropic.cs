using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.Anthropic;

public sealed class ProviderAnthropic() : BaseProvider(LLMProviders.ANTHROPIC, "https://api.anthropic.com/v1/", LOGGER)
{
    private static readonly ILogger<ProviderAnthropic> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderAnthropic>();

    #region Implementation of IProvider

    public override string Id => LLMProviders.ANTHROPIC.ToName();

    public override string InstanceName { get; set; } = "Anthropic";

    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        // Get the API key:
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this, SecretStoreType.LLM_PROVIDER);
        if(!requestedSecret.Success)
            yield break;
        
        // Parse the API parameters:
        var apiParameters = this.ParseAdditionalApiParameters("system");

        // Build the list of messages:
        var messages = await chatThread.Blocks.BuildMessagesAsync(
            this.Provider, chatModel,
            
            // Anthropic-specific role mapping:
            role => role switch
            {
                ChatRole.USER => "user",
                ChatRole.AI => "assistant",
                ChatRole.AGENT => "assistant",

                _ => "user",
            },
            
            // Anthropic uses the standard text sub-content:
            text => new SubContentText
            {
                Text = text,
            },
            
            // Anthropic-specific image sub-content:
            async attachment => new SubContentImage
            {
                Source = new SubContentBase64Image
                {
                    Data = await attachment.TryAsBase64(token: token) is (true, var base64Content)
                        ? base64Content
                        : string.Empty,
                    
                    MediaType = attachment.DetermineMimeType(),
                }
            }
        );
        
        // Prepare the Anthropic HTTP chat request:
        var chatRequest = JsonSerializer.Serialize(new ChatRequest
        {
            Model = chatModel.Id,
            
            // Build the messages:
            Messages = [..messages],
            
            System = chatThread.PrepareSystemPrompt(settingsManager),
            MaxTokens = apiParameters.TryGetValue("max_tokens", out var value) && value is int intValue ? intValue : 4_096,
            
            // Right now, we only support streaming completions:
            Stream = true,
            AdditionalApiParameters = apiParameters
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
        
        await foreach (var content in this.StreamChatCompletionInternal<ResponseStreamLine, NoChatCompletionAnnotationStreamLine>("Anthropic", RequestBuilder, token))
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
        
        return this.LoadModels(SecretStoreType.LLM_PROVIDER, token, apiKeyProvisional).ContinueWith(t => t.Result.Concat(additionalModels).OrderBy(x => x.Id).AsEnumerable(), token);
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