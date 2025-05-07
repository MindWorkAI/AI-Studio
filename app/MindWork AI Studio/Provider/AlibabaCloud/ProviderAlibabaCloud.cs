using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;

namespace AIStudio.Provider.AlibabaCloud;

public sealed class ProviderAlibabaCloud(ILogger logger) : BaseProvider("https://dashscope-intl.aliyuncs.com/compatible-mode/v1/", logger)
{

    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.ALIBABA_CLOUD.ToName();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "AlibabaCloud";
    
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
        
        // Prepare the AlibabaCloud HTTP chat request:
        var alibabaCloudChatRequest = JsonSerializer.Serialize(new ChatRequest
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
            request.Content = new StringContent(alibabaCloudChatRequest, Encoding.UTF8, "application/json");
            return request;
        }
        
        await foreach (var content in this.StreamChatCompletionInternal<ResponseStreamLine>("AlibabaCloud", RequestBuilder, token))
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
            new Model("qwq-plus", "QwQ plus"), // reasoning model 
            new Model("qwen-max-latest", "Qwen-Max (Latest)"),
            new Model("qwen-plus-latest", "Qwen-Plus (Latest)"),
            new Model("qwen-turbo-latest", "Qwen-Turbo (Latest)"),
            new Model("qvq-max", "QVQ Max"), // visual reasoning model 
            new Model("qvq-max-latest", "QVQ Max (Latest)"), // visual reasoning model 
            new Model("qwen-vl-max", "Qwen-VL Max"), // text generation model that can understand and process images
            new Model("qwen-vl-plus", "Qwen-VL Plus"), // text generation model that can understand and process images
            new Model("qwen-mt-plus", "Qwen-MT Plus"), // machine translation
            new Model("qwen-mt-turbo", "Qwen-MT Turbo"), // machine translation
            
            //Open source
            new Model("qwen2.5-14b-instruct-1m", "Qwen2.5 14b 1m context"), 
            new Model("qwen2.5-7b-instruct-1m", "Qwen2.5 7b 1m context"),
            new Model("qwen2.5-72b-instruct", "Qwen2.5 72b"),  
            new Model("qwen2.5-32b-instruct", "Qwen2.5 32b"),  
            new Model("qwen2.5-14b-instruct", "Qwen2.5 14b"),  
            new Model("qwen2.5-7b-instruct", "Qwen2.5 7b"),  
            new Model("qwen2.5-omni-7b", "Qwen2.5-Omni 7b"), // omni-modal understanding and generation model
            new Model("qwen2.5-vl-72b-instruct", "Qwen2.5-VL 72b"),  
            new Model("qwen2.5-vl-32b-instruct", "Qwen2.5-VL 32b"),  
            new Model("qwen2.5-vl-7b-instruct", "Qwen2.5-VL 7b"),  
            new Model("qwen2.5-vl-3b-instruct", "Qwen2.5-VL 3b"),  
        };
        
        return this.LoadModels(["q"],token, apiKeyProvisional).ContinueWith(t => t.Result.Concat(additionalModels).OrderBy(x => x.Id).AsEnumerable(), token);
    }

    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(Enumerable.Empty<Model>());
    }
    
    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        
        var additionalModels = new[]
        {
            new Model("text-embedding-v3", "text-embedding-v3"),
        };
        
        return this.LoadModels(["text-embedding-"], token, apiKeyProvisional).ContinueWith(t => t.Result.Concat(additionalModels).OrderBy(x => x.Id).AsEnumerable(), token);
    }
    
    /// <inheritdoc />
    public override IReadOnlyCollection<Capability> GetModelCapabilities(Model model)
    {
        var modelName = model.Id.ToLowerInvariant().AsSpan();
        
        // Qwen models:
        if (modelName.StartsWith("qwen"))
        {
            // Check for omni models:
            if (modelName.IndexOf("omni") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.AUDIO_INPUT, Capability.SPEECH_INPUT,
                    Capability.VIDEO_INPUT,

                    Capability.TEXT_OUTPUT, Capability.SPEECH_OUTPUT
                ];
            
            // Check for Qwen 3:
            if(modelName.StartsWith("qwen3"))
                return
                [
                    Capability.TEXT_INPUT,
                    Capability.TEXT_OUTPUT,
                    
                    Capability.OPTIONAL_REASONING, Capability.FUNCTION_CALLING
                ];
            
            if(modelName.IndexOf("-vl-") is not -1)
                return
                [
                    Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                    Capability.TEXT_OUTPUT,
                ];
        }
        
        // QwQ models:
        if (modelName.StartsWith("qwq"))
        {
            return
            [
                Capability.TEXT_INPUT, 
                Capability.TEXT_OUTPUT,
                
                Capability.ALWAYS_REASONING, Capability.FUNCTION_CALLING
            ];
        }
        
        // QVQ models:
        if (modelName.StartsWith("qvq"))
        {
            return
            [
                Capability.TEXT_INPUT, Capability.MULTIPLE_IMAGE_INPUT,
                Capability.TEXT_OUTPUT,
                
                Capability.ALWAYS_REASONING
            ];
        }

        // Default to text input and output:
        return
        [
            Capability.TEXT_INPUT,
            Capability.TEXT_OUTPUT,
            
            Capability.FUNCTION_CALLING
        ];
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