using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.Google;

public class ProviderGoogle() : BaseProvider(LLMProviders.GOOGLE, "https://generativelanguage.googleapis.com/v1beta/openai/", LOGGER)
{
    private static readonly ILogger<ProviderGoogle> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderGoogle>();

    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.GOOGLE.ToName();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "Google Gemini";

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
        var messages = await chatThread.Blocks.BuildMessagesUsingNestedImageUrlAsync(this.Provider, chatModel);
        
        // Prepare the Google HTTP chat request:
        var geminiChatRequest = JsonSerializer.Serialize(new ChatRequest
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

            // Set the content:
            request.Content = new StringContent(geminiChatRequest, Encoding.UTF8, "application/json");
            return request;
        }
        
        await foreach (var content in this.StreamChatCompletionInternal<ChatCompletionDeltaStreamLine, NoChatCompletionAnnotationStreamLine>("Google", RequestBuilder, token))
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
    public override Task<string> TranscribeAudioAsync(Provider.Model transcriptionModel, string audioFilePath, SettingsManager settingsManager, CancellationToken token = default)
    {
        return Task.FromResult(string.Empty);
    }

    /// <inheritdoc />
    public override async Task<IEnumerable<Provider.Model>> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var modelResponse = await this.LoadModels(token, apiKeyProvisional);
        if(modelResponse == default)
            return [];
        
        return modelResponse.Models.Where(model =>
                model.Name.StartsWith("models/gemini-", StringComparison.OrdinalIgnoreCase) && !model.Name.Contains("embed"))
            .Select(n => new Provider.Model(n.Name.Replace("models/", string.Empty), n.DisplayName));
    }

    /// <inheritdoc />
    public override Task<IEnumerable<Provider.Model>> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(Enumerable.Empty<Provider.Model>());
    }

    public override async Task<IEnumerable<Provider.Model>> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var modelResponse = await this.LoadModels(token, apiKeyProvisional);
        if(modelResponse == default)
            return [];
        
        return modelResponse.Models.Where(model =>
                model.Name.StartsWith("models/text-embedding-", StringComparison.OrdinalIgnoreCase) ||
                model.Name.StartsWith("models/gemini-embed", StringComparison.OrdinalIgnoreCase))
            .Select(n => new Provider.Model(n.Name.Replace("models/", string.Empty), n.DisplayName));
    }
    
    /// <inheritdoc />
    public override Task<IEnumerable<Provider.Model>> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(Enumerable.Empty<Provider.Model>());
    }
    
    #endregion

    private async Task<ModelsResponse> LoadModels(CancellationToken token, string? apiKeyProvisional = null)
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

        using var request = new HttpRequestMessage(HttpMethod.Get, $"models?key={secretKey}");
        using var response = await this.httpClient.SendAsync(request, token);
        
        if(!response.IsSuccessStatusCode)
            return default;

        var modelResponse = await response.Content.ReadFromJsonAsync<ModelsResponse>(token);
        return modelResponse;
    }
}