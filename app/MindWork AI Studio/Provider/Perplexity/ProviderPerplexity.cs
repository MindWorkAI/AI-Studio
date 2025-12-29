using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.Perplexity;

public sealed class ProviderPerplexity() : BaseProvider(LLMProviders.PERPLEXITY, "https://api.perplexity.ai/", LOGGER)
{
    private static readonly ILogger<ProviderPerplexity> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderPerplexity>();

    private static readonly Model[] KNOWN_MODELS =
    [
        new("sonar", "Sonar"),
        new("sonar-pro", "Sonar Pro"),
        new("sonar-reasoning", "Sonar Reasoning"),
        new("sonar-reasoning-pro", "Sonar Reasoning Pro"),
        new("sonar-deep-research", "Sonar Deep Research"),
    ];
    
    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.PERPLEXITY.ToName();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "Perplexity";
    
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
        var messages = await chatThread.Blocks.BuildMessagesUsingStandardsAsync(this.Provider, chatModel);
        
        // Prepare the Perplexity HTTP chat request:
        var perplexityChatRequest = JsonSerializer.Serialize(new ChatCompletionAPIRequest
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
            request.Content = new StringContent(perplexityChatRequest, Encoding.UTF8, "application/json");
            return request;
        }
        
        await foreach (var content in this.StreamChatCompletionInternal<ResponseStreamLine, NoChatCompletionAnnotationStreamLine>("Perplexity", RequestBuilder, token))
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
        return this.LoadModels();
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
    
    #endregion

    private Task<IEnumerable<Model>> LoadModels() => Task.FromResult<IEnumerable<Model>>(KNOWN_MODELS);
}