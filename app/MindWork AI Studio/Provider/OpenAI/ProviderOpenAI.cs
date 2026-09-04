using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.Harness;
using AIStudio.Tools.Services;

using Microsoft.Extensions.DependencyInjection;

namespace AIStudio.Provider.OpenAI;

/// <summary>
/// The OpenAI provider.
/// </summary>
public sealed class ProviderOpenAI() : BaseProvider(LLMProviders.OPEN_AI, new Uri("https://api.openai.com/v1/"), ExternalHttpTrustPolicy.SYSTEM_TRUST_ONLY, LOGGER)
{
    private static readonly ILogger<ProviderOpenAI> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderOpenAI>();
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ProviderOpenAI).Namespace, nameof(ProviderOpenAI));
    
    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.OPEN_AI.ToSecretId();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "OpenAI";

    /// <inheritdoc />
    public override bool HasModelLoadingCapability => true;

    protected override ProviderRequestFailureReason ClassifyProviderRequestFailure(HttpStatusCode statusCode, string responseBody)
    {
        if (statusCode is HttpStatusCode.TooManyRequests && HasInsufficientQuotaError(responseBody))
            return ProviderRequestFailureReason.INSUFFICIENT_QUOTA;

        return base.ClassifyProviderRequestFailure(statusCode, responseBody);
    }

    protected override ProviderRequestFailureReason ClassifyProviderRequestFailure(string? errorCode, string? errorType, string? errorMessage, string responseBody)
    {
        if (IsInsufficientQuota(errorCode) || IsInsufficientQuota(errorType) || HasInsufficientQuotaError(responseBody))
            return ProviderRequestFailureReason.INSUFFICIENT_QUOTA;

        return base.ClassifyProviderRequestFailure(errorCode, errorType, errorMessage, responseBody);
    }

    protected override string GetProviderRequestFailureUserMessage(ProviderRequestFailureReason failureReason) => failureReason switch
    {
        ProviderRequestFailureReason.INSUFFICIENT_QUOTA => TB("It looks like you do not have any API credits left with OpenAI. Please add credits to your account and try again."),
        _ => base.GetProviderRequestFailureUserMessage(failureReason),
    };

    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        // Get the API key:
        var requestedSecret = await Program.RUST_SERVICE.GetAPIKey(this, SecretStoreType.LLM_PROVIDER);
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

        // Read the model capabilities. Through the settings provider, so that the user's expert
        // capability overrides apply:
        var providerSettings = this.CreateSettingsProvider(chatModel);
        var modelCapabilities = providerSettings.GetModelCapabilities();
        
        // Check if we are using the Responses API or the Chat Completion API:
        var usingResponsesAPI = modelCapabilities.Contains(Capability.RESPONSES_API);
        
        // Prepare the request path based on the API we are using:
        var requestPath = usingResponsesAPI ? "responses" : "chat/completions";
        
        LOGGER.LogInformation("Using the system prompt role '{SystemPromptRole}' and the '{RequestPath}' API for model '{ChatModelId}'.", systemPromptRole, requestPath, chatModel.Id);
        
        //
        // Prepare the tools we want to use:
        //
        var toolRegistry = Program.SERVICE_PROVIDER.GetService<ToolRegistry>();
        var providerConfidence = this.Provider.GetConfidence(settingsManager).Level;

        //
        // The provider-native web search is held to the same confidence the local web search tool
        // asks for: to the user it is the same act, whoever performs the search.
        //
        var minimumWebSearchConfidence = toolRegistry?.GetMinimumProviderConfidence(ToolSelectionRules.WEB_SEARCH_TOOL_ID) ?? ConfidenceLevel.NONE;
        var isWebSearchAllowed = settingsManager.IsToolActive(ToolSelectionRules.WEB_SEARCH_TOOL_ID) &&
                                 ToolSelectionRules.IsProviderConfidenceAllowed(providerConfidence, minimumWebSearchConfidence);
        IList<object> providerTools = modelCapabilities.Contains(Capability.WEB_SEARCH) && isWebSearchAllowed
            ? [ ProviderTools.WEB_SEARCH ]
            : [];
        
        
        // Parse the API parameters:
        var apiParameters = this.ParseAdditionalApiParameters("input", "store", "tools");

        if (!usingResponsesAPI)
        {
            await foreach (var content in this.StreamOpenAICompatibleChatCompletion<ChatCompletionAPIRequest, ChatCompletionDeltaStreamLine, ChatCompletionAnnotationStreamLine>(
                               "OpenAI",
                               chatModel,
                               chatThread,
                               settingsManager,
                               async (systemPrompt, apiParameters, tools) =>
                               {
                                   var messages = await chatThread.Blocks.BuildMessagesAsync(
                                       this.Provider,
                                       chatModel,
                                       role => role switch
                                       {
                                           ChatRole.USER => "user",
                                           ChatRole.AI => "assistant",
                                           ChatRole.AGENT => "assistant",
                                           ChatRole.SYSTEM => systemPromptRole,
                                           _ => "user",
                                       },
                                       text => new SubContentText
                                       {
                                           Text = text,
                                       },
                                       async attachment => new SubContentImageUrlNested
                                       {
                                           ImageUrl = new SubContentImageUrlData
                                           {
                                               Url = await attachment.TryAsBase64(token: token) is (true, var base64Content)
                                                   ? $"data:{attachment.DetermineMimeType()};base64,{base64Content}"
                                                   : string.Empty,
                                           },
                                       });

                                   return new ChatCompletionAPIRequest
                                   {
                                       Model = chatModel.Id,
                                       Messages = [systemPrompt, ..messages],
                                       Stream = true,
                                       Tools = tools,
                                       AdditionalApiParameters = apiParameters,
                                   };
                               },
                               systemPromptRole: systemPromptRole,
                               requestPath: "chat/completions",
                               token: token))
                yield return content;

            yield break;
        }

        var toolExecutor = Program.SERVICE_PROVIDER.GetService<ToolExecutor>();
        var currentAssistantContent = chatThread.Blocks.LastOrDefault(x => x.Role is ChatRole.AI)?.Content as ContentText;
        currentAssistantContent?.ToolInvocations.Clear();

        IReadOnlyList<(ToolDefinition Definition, IToolImplementation Implementation)> runnableTools = toolRegistry is null
            ? []
            : await toolRegistry.GetRunnableToolsAsync(
                providerSettings,
                chatThread.RuntimeComponent,
                chatThread.RuntimeSelectedToolIds,
                providerConfidence,
                chatThread.MayRunTools(settingsManager));

        var toolAwareDefinitions = toolExecutor is null
            ? Enumerable.Empty<ToolDefinition>()
            : runnableTools.Select(x => x.Definition);
        var systemPrompt = new TextMessage
        {
            Role = systemPromptRole,
            Content = chatThread.PrepareSystemPrompt(settingsManager, toolAwareDefinitions),
        };

        // Build the list of messages:
        var messages = await chatThread.Blocks.BuildMessagesAsync(
            this.Provider, chatModel,
            role => role switch
            {
                ChatRole.USER => "user",
                ChatRole.AI => "assistant",
                ChatRole.AGENT => "assistant",
                ChatRole.SYSTEM => systemPromptRole,
                _ => "user",
            },
            text => new SubContentInputText
            {
                Text = text,
            },
            async attachment => new SubContentInputImage
            {
                ImageUrl = await attachment.TryAsBase64(token: token) is (true, var base64Content)
                    ? $"data:{attachment.DetermineMimeType()};base64,{base64Content}"
                    : string.Empty,
            });

        var baseInput = new List<object> { systemPrompt };
        baseInput.AddRange(messages.Cast<object>());

        if (usingResponsesAPI && toolExecutor is not null && runnableTools.Count > 0)
        {
            var adapter = new ResponsesToolCallingAdapter(
                chatModel,
                baseInput,
                apiParameters,
                providerTools,
                runnableTools,
                (requestDto, requestToken) => this.ExecuteResponsesRequest(requestDto, requestedSecret, requestToken));

            var loop = Program.SERVICE_PROVIDER.GetRequiredService<IToolCallingLoop>();
            var loopContext = new ToolCallingLoopContext
            {
                ChatThread = chatThread,
                RunnableTools = runnableTools,
                ToolExecutor = toolExecutor,
                Provider = this,
                CurrentAssistantContent = currentAssistantContent,
                ProviderInstanceName = this.InstanceName,
                ProviderType = this.Provider,
                ModelId = chatModel.Id,
            };

            await foreach (var content in loop.RunAsync(adapter, loopContext, token))
                yield return content;

            yield break;
        }

        if (runnableTools.Count > 0)
            providerTools = [];
        
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
                Input = baseInput,
            
                // Right now, we only support streaming completions:
                Stream = true,
                
                // We do not want to store any data on OpenAI's servers:
                Store = false,
                
                // Tools we want to use:
                Tools = providerTools,
                
                // Additional API parameters:
                AdditionalApiParameters = apiParameters
                
            }, JSON_SERIALIZER_OPTIONS),
        };
        
        async Task<HttpRequestMessage> RequestBuilder()
        {
            // Build the HTTP post request:
            var request = new HttpRequestMessage(HttpMethod.Post, requestPath);

            // Set the authorization header:
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await requestedSecret.Secret.Decrypt(Program.ENCRYPTION));

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

    private async Task<ResponsesResponse?> ExecuteResponsesRequest(ResponsesAPIRequest requestDto, RequestedSecret requestedSecret, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await requestedSecret.Secret.Decrypt(Program.ENCRYPTION));
        request.Content = new StringContent(JsonSerializer.Serialize(requestDto, JSON_SERIALIZER_OPTIONS), Encoding.UTF8, "application/json");

        using var response = await this.HttpClient.SendAsync(request, token);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(token);
            LOGGER.LogError("Tool calling Responses API request failed with status code {ResponseStatusCode} and body: '{ResponseBody}'.", response.StatusCode, responseBody);
            await ToolCallingMessages.SendToolCallingRequestFailedAsync((int)response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ResponsesResponse>(JSON_SERIALIZER_OPTIONS, token);
    }

    #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    
    /// <inheritdoc />
    public override async IAsyncEnumerable<ImageURL> StreamImageCompletion(Model imageModel, string promptPositive, string promptNegative = FilterOperator.String.Empty, ImageURL referenceImageURL = default, [EnumeratorCancellation] CancellationToken token = default)
    {
        yield break;
    }
    
    #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    
    /// <inheritdoc />
    public override async Task<TranscriptionResult> TranscribeAudioAsync(Model transcriptionModel, string audioFilePath, SettingsManager settingsManager, CancellationToken token = default)
    {
        var requestedSecret = await Program.RUST_SERVICE.GetAPIKey(this, SecretStoreType.TRANSCRIPTION_PROVIDER);
        return await this.PerformStandardTranscriptionRequest(requestedSecret, transcriptionModel, audioFilePath, token: token);
    }
    
    /// <inhertidoc />
    public override async Task<IReadOnlyList<IReadOnlyList<float>>> EmbedTextAsync(Model embeddingModel, SettingsManager settingsManager, CancellationToken token = default, params List<string> texts)
    {
        var requestedSecret = await Program.RUST_SERVICE.GetAPIKey(this, SecretStoreType.EMBEDDING_PROVIDER);
        return await this.PerformStandardTextEmbeddingRequest(requestedSecret, embeddingModel, token: token, texts: texts);
    }

    //
    // OpenAI offers every kind of model through one models endpoint, so we have to sort them apart
    // ourselves. We used to do that with lists of name prefixes kept here. The shared model kind
    // detection knows those families as well, and it knows them for every provider, so we ask it
    // instead of maintaining a second set of rules which only ever lagged behind.
    //

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModels(SecretStoreType.LLM_PROVIDER, static model => model.IsChatModel(), token, apiKeyProvisional);
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModels(SecretStoreType.IMAGE_PROVIDER, static model => model.IsImageModel(), token, apiKeyProvisional);
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModels(SecretStoreType.EMBEDDING_PROVIDER, static model => model.IsEmbeddingModel(), token, apiKeyProvisional);
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModels(SecretStoreType.TRANSCRIPTION_PROVIDER, static model => model.IsTranscriptionModel(), token, apiKeyProvisional);
    }
    
    #endregion

    private Task<ModelLoadResult> LoadModels(SecretStoreType storeType, Func<Model, bool> isWantedKind, CancellationToken token, string? apiKeyProvisional = null)
    {
        return this.LoadModelsResponse<ModelsResponse>(
            storeType,
            "models",
            modelResponse => modelResponse.Data.Where(isWantedKind),
            token,
            apiKeyProvisional);
    }

    private static bool HasInsufficientQuotaError(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return false;

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return HasInsufficientQuotaError(document.RootElement);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HasInsufficientQuotaError(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (HasJsonStringValue(element, "type", "insufficient_quota") ||
                    HasJsonStringValue(element, "code", "insufficient_quota"))
                    return true;

                foreach (var property in element.EnumerateObject())
                    if (HasInsufficientQuotaError(property.Value))
                        return true;

                return false;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    if (HasInsufficientQuotaError(item))
                        return true;

                return false;

            default:
                return false;
        }
    }

    private static bool IsInsufficientQuota(string? value)
    {
        return value is not null && value.Equals("insufficient_quota", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasJsonStringValue(JsonElement element, string propertyName, string expectedValue)
    {
        return element.TryGetProperty(propertyName, out var propertyElement) &&
               propertyElement.ValueKind is JsonValueKind.String &&
               string.Equals(propertyElement.GetString(), expectedValue, StringComparison.OrdinalIgnoreCase);
    }
}
