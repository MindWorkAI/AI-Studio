using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.Perplexity;

public sealed class ProviderPerplexity(ILogger logger) : BaseProvider("https://api.perplexity.ai/", logger)
{
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
        
        // Prepare the Perplexity HTTP chat request:
        var perplexityChatRequest = JsonSerializer.Serialize(new ChatRequest
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
            Stream = true,
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
        
        await foreach (var content in this.StreamChatCompletionInternal<ResponseStreamLine>("Perplexity", RequestBuilder, token))
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
    
    public override IReadOnlyCollection<Capability> GetModelCapabilities(Model model)
    {
        var modelName = model.Id.ToLowerInvariant().AsSpan();
        
        if(modelName.IndexOf("reasoning") is not -1 ||
           modelName.IndexOf("deep-research") is not -1)
            return
            [
                Capability.TEXT_INPUT,
                Capability.MULTIPLE_IMAGE_INPUT,
                
                Capability.TEXT_OUTPUT,
                Capability.IMAGE_OUTPUT,
                
                Capability.ALWAYS_REASONING,
            ];
        
        return
        [
            Capability.TEXT_INPUT,
            Capability.MULTIPLE_IMAGE_INPUT,
            
            Capability.TEXT_OUTPUT,
            Capability.IMAGE_OUTPUT,
        ];
    }
    
    #endregion

    private Task<IEnumerable<Model>> LoadModels() => Task.FromResult<IEnumerable<Model>>(KNOWN_MODELS);
}