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
public sealed class ProviderOpenAI() : BaseProvider(LLMProviders.OPEN_AI, "https://api.openai.com/v1/", LOGGER)
{
    private static readonly ILogger<ProviderOpenAI> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderOpenAI>();
    
    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.OPEN_AI.ToName();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "OpenAI";

    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        // Get the API key:
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this, SecretStoreType.LLM_PROVIDER);
        if(!requestedSecret.Success)
            yield break;
        
        // Unfortunately, OpenAI changed the name of the system prompt based on the model.
        // All models that start with "o" (the omni aka reasoning models), all GPT4o models,
        // and all newer models have the system prompt named "developer". All other models
        // have the system prompt named "system". We need to check this to get the correct
        // system prompt.
        //
        // To complicate it even more: The early versions of reasoning models, which are released
        // before the 17th of December 2024, have no system prompt at all. We need to check this
        // as well.
        
        // Apply the basic rule first:
        var systemPromptRole =
            chatModel.Id.StartsWith('o') ||
            chatModel.Id.StartsWith("gpt-5", StringComparison.Ordinal) ||
            chatModel.Id.Contains("4o") ? "developer" : "system";
        
        // Check if the model is an early version of the reasoning models:
        systemPromptRole = chatModel.Id switch
        {
            "o1-mini" => "user",
            "o1-mini-2024-09-12" => "user",
            "o1-preview" => "user",
            "o1-preview-2024-09-12" => "user",
            
            _ => systemPromptRole,
        };

        // Read the model capabilities:
        var modelCapabilities = this.Provider.GetModelCapabilities(chatModel);
        
        // Check if we are using the Responses API or the Chat Completion API:
        var usingResponsesAPI = modelCapabilities.Contains(Capability.RESPONSES_API);
        
        // Prepare the request path based on the API we are using:
        var requestPath = usingResponsesAPI ? "responses" : "chat/completions";
        
        LOGGER.LogInformation("Using the system prompt role '{SystemPromptRole}' and the '{RequestPath}' API for model '{ChatModelId}'.", systemPromptRole, requestPath, chatModel.Id);
        
        // Prepare the system prompt:
        var systemPrompt = new TextMessage
        {
            Role = systemPromptRole,
            Content = chatThread.PrepareSystemPrompt(settingsManager),
        };

        //
        // Prepare the tools we want to use:
        //
        IList<Tool> tools = modelCapabilities.Contains(Capability.WEB_SEARCH) switch
        {
            true => [ Tools.WEB_SEARCH ],
            _ => []
        };
        
        
        // Parse the API parameters:
        var apiParameters = this.ParseAdditionalApiParameters("input", "store", "tools");

        // Build the list of messages:
        var messages = await chatThread.Blocks.BuildMessagesAsync(
            this.Provider, chatModel,

            // OpenAI-specific role mapping:
            role => role switch
            {
                ChatRole.USER => "user",
                ChatRole.AI => "assistant",
                ChatRole.AGENT => "assistant",
                ChatRole.SYSTEM => systemPromptRole,

                _ => "user",
            },

            // OpenAI's text sub-content depends on the model, whether we are using
            // the Responses API or the Chat Completion API:
            text => usingResponsesAPI switch
            {
                // Responses API uses INPUT_TEXT:
                true => new SubContentInputText
                {
                    Text = text,
                },

                // Chat Completion API uses TEXT:
                false => new SubContentText
                {
                    Text = text,
                },
            },

            // OpenAI's image sub-content depends on the model as well,
            // whether we are using the Responses API or the Chat Completion API:
            async attachment => usingResponsesAPI switch
            {
                // Responses API uses INPUT_IMAGE:
                true => new SubContentInputImage
                {
                    ImageUrl = await attachment.TryAsBase64(token: token) is (true, var base64Content)
                        ? $"data:{attachment.DetermineMimeType()};base64,{base64Content}"
                        : string.Empty,
                },
                
                // Chat Completion API uses IMAGE_URL:
                false => new SubContentImageUrlNested
                {
                    ImageUrl = new SubContentImageUrlData
                    {
                        Url = await attachment.TryAsBase64(token: token) is (true, var base64Content)
                            ? $"data:{attachment.DetermineMimeType()};base64,{base64Content}"
                            : string.Empty,
                    },
                }
            });
        
        //
        // Create the request: either for the Responses API or the Chat Completion API
        //
        var openAIChatRequest = usingResponsesAPI switch
        {
            // Chat Completion API request:
            false => JsonSerializer.Serialize(new ChatCompletionAPIRequest
            {
                Model = chatModel.Id,
            
                // All messages go into the messages field:
                Messages = [systemPrompt, ..messages],
            
                // Right now, we only support streaming completions:
                Stream = true,
                AdditionalApiParameters = apiParameters
            }, JSON_SERIALIZER_OPTIONS),
            
            // Responses API request:
            true => JsonSerializer.Serialize(new ResponsesAPIRequest
            {
                Model = chatModel.Id,
            
                // All messages go into the input field:
                Input = [systemPrompt, ..messages],
            
                // Right now, we only support streaming completions:
                Stream = true,
                
                // We do not want to store any data on OpenAI's servers:
                Store = false,
                
                // Tools we want to use:
                Tools = tools,
                
                // Additional API parameters:
                AdditionalApiParameters = apiParameters
                
            }, JSON_SERIALIZER_OPTIONS),
        };
        
        async Task<HttpRequestMessage> RequestBuilder()
        {
            // Build the HTTP post request:
            var request = new HttpRequestMessage(HttpMethod.Post, requestPath);

            // Set the authorization header:
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await requestedSecret.Secret.Decrypt(ENCRYPTION));

            // Set the content:
            request.Content = new StringContent(openAIChatRequest, Encoding.UTF8, "application/json");
            return request;
        }

        if (usingResponsesAPI)
            await foreach (var content in this.StreamResponsesInternal<ResponsesDeltaStreamLine, ResponsesAnnotationStreamLine>("OpenAI", RequestBuilder, token))
                yield return content;
        
        else
            await foreach (var content in this.StreamChatCompletionInternal<ChatCompletionDeltaStreamLine, ChatCompletionAnnotationStreamLine>("OpenAI", RequestBuilder, token))
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
    public override async Task<string> TranscribeAudioAsync(Model transcriptionModel, string audioFilePath, SettingsManager settingsManager, CancellationToken token = default)
    {
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this, SecretStoreType.TRANSCRIPTION_PROVIDER);
        return await this.PerformStandardTranscriptionRequest(requestedSecret, transcriptionModel, audioFilePath, token: token);
    }
    
    /// <inhertidoc />
    public override async Task<IReadOnlyList<IReadOnlyList<float>>> EmbedTextAsync(Provider.Model embeddingModel, SettingsManager settingsManager, CancellationToken token = default, params List<string> texts)
    {
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this, SecretStoreType.EMBEDDING_PROVIDER);
        return await this.PerformStandardTextEmbeddingRequest(requestedSecret, embeddingModel, token: token, texts: texts);
    }

    /// <inheritdoc />
    public override async Task<IEnumerable<Model>> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var models = await this.LoadModels(SecretStoreType.LLM_PROVIDER, ["chatgpt-", "gpt-", "o1-", "o3-", "o4-"], token, apiKeyProvisional);
        return models.Where(model => !model.Id.Contains("image", StringComparison.OrdinalIgnoreCase) &&
                                     !model.Id.Contains("realtime", StringComparison.OrdinalIgnoreCase) &&
                                     !model.Id.Contains("audio", StringComparison.OrdinalIgnoreCase) &&
                                     !model.Id.Contains("tts", StringComparison.OrdinalIgnoreCase) &&
                                     !model.Id.Contains("transcribe", StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModels(SecretStoreType.IMAGE_PROVIDER, ["dall-e-", "gpt-image"], token, apiKeyProvisional);
    }
    
    /// <inheritdoc />
    public override Task<IEnumerable<Model>> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModels(SecretStoreType.EMBEDDING_PROVIDER, ["text-embedding-"], token, apiKeyProvisional);
    }
    
    /// <inheritdoc />
    public override async Task<IEnumerable<Model>> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var models = await this.LoadModels(SecretStoreType.TRANSCRIPTION_PROVIDER, ["whisper-", "gpt-"], token, apiKeyProvisional);
        return models.Where(model => model.Id.StartsWith("whisper-", StringComparison.InvariantCultureIgnoreCase) ||
                                     model.Id.Contains("-transcribe", StringComparison.InvariantCultureIgnoreCase));
    }
    
    #endregion

    private async Task<IEnumerable<Model>> LoadModels(SecretStoreType storeType, string[] prefixes, CancellationToken token, string? apiKeyProvisional = null)
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
        
        using var request = new HttpRequestMessage(HttpMethod.Get, "models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);

        using var response = await this.httpClient.SendAsync(request, token);
        if(!response.IsSuccessStatusCode)
            return [];

        var modelResponse = await response.Content.ReadFromJsonAsync<ModelsResponse>(token);
        return modelResponse.Data.Where(model => prefixes.Any(prefix => model.Id.StartsWith(prefix, StringComparison.InvariantCulture)));
    }
}