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

        // Read the model capabilities:
        var modelCapabilities = this.Provider.GetModelCapabilities(chatModel);
        
        // Check if we are using the Responses API or the Chat Completion API:
        var usingResponsesAPI = modelCapabilities.Contains(Capability.RESPONSES_API);
        
        // Prepare the request path based on the API we are using:
        var requestPath = usingResponsesAPI ? "responses" : "chat/completions";
        
        LOGGER.LogInformation("Using the system prompt role '{SystemPromptRole}' and the '{RequestPath}' API for model '{ChatModelId}'.", systemPromptRole, requestPath, chatModel.Id);
        
        //
        // Prepare the tools we want to use:
        //
        var providerConfidence = this.Provider.GetConfidence(settingsManager).Level;
        var minimumWebSearchConfidence = settingsManager.GetMinimumProviderConfidenceForTool(ToolSelectionRules.WEB_SEARCH_TOOL_ID);
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

        var toolRegistry = Program.SERVICE_PROVIDER.GetService<ToolRegistry>();
        var toolExecutor = Program.SERVICE_PROVIDER.GetService<ToolExecutor>();
        var currentAssistantContent = chatThread.Blocks.LastOrDefault(x => x.Role is ChatRole.AI)?.Content as ContentText;
        currentAssistantContent?.ToolInvocations.Clear();

        IReadOnlyList<(ToolDefinition Definition, IToolImplementation Implementation)> runnableTools = toolRegistry is null
            ? []
            : await toolRegistry.GetRunnableToolsAsync(
                new AIStudio.Settings.Provider
                {
                    UsedLLMProvider = this.Provider,
                    Model = chatModel,
                    InstanceName = this.InstanceName,
                },
                chatThread.RuntimeComponent,
                chatThread.RuntimeSelectedToolIds,
                modelCapabilities,
                providerConfidence,
                settingsManager.IsToolSelectionVisible(chatThread.RuntimeComponent));

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
            await foreach (var content in this.StreamResponsesWithLocalTools(
                               chatModel,
                               chatThread,
                               baseInput,
                               apiParameters,
                               providerTools,
                               runnableTools,
                               toolExecutor,
                               currentAssistantContent,
                               requestedSecret,
                               token))
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

    private async IAsyncEnumerable<ContentStreamChunk> StreamResponsesWithLocalTools(
        Model chatModel,
        ChatThread chatThread,
        IList<object> baseInput,
        IDictionary<string, object> apiParameters,
        IList<object> providerTools,
        IReadOnlyList<(ToolDefinition Definition, IToolImplementation Implementation)> runnableTools,
        ToolExecutor toolExecutor,
        ContentText? currentAssistantContent,
        RequestedSecret requestedSecret,
        [EnumeratorCancellation] CancellationToken token)
    {
        var localProviderTools = runnableTools
            .Select(x => (object)ProviderToolAdapters.ToResponsesTool(x.Definition))
            .ToList();
        var localFunctionNames = runnableTools
            .Select(x => x.Definition.Function.Name)
            .ToHashSet(StringComparer.Ordinal);
        var effectiveProviderTools = providerTools
            .Where(x => x is not ProviderTool providerTool || !localFunctionNames.Contains(providerTool.Type))
            .Concat(localProviderTools)
            .ToList();
        // Preserve every output item required to continue the response, including
        // reasoning items emitted alongside function calls.
        var internalItems = new List<object>();
        var toolCallCount = 0;
        var toolResultCharacterCount = 0L;
        var toolSources = new List<Source>();

        while (true)
        {
            var finalResponseInstruction = ToolSelectionRules.GetToolCallsUnavailableInstruction(toolCallCount, toolResultCharacterCount);
            var finalResponseRequired = finalResponseInstruction is not null;
            var requestInput = new List<object>(baseInput);
            if (finalResponseRequired && requestInput.FirstOrDefault() is TextMessage systemPrompt)
            {
                requestInput[0] = systemPrompt with
                {
                    Content = $"{systemPrompt.Content}{Environment.NewLine}{Environment.NewLine}{finalResponseInstruction}",
                };
            }
            requestInput.AddRange(internalItems);

            var requestDto = new ResponsesAPIRequest
            {
                Model = chatModel.Id,
                Input = requestInput,
                Stream = false,
                Store = false,
                Tools = finalResponseRequired ? [] : effectiveProviderTools,
                AdditionalApiParameters = apiParameters,
            };
            var response = await this.ExecuteResponsesRequest(requestDto, requestedSecret, token);
            if (response is null)
            {
                await ResetToolRuntimeStatusAsync(currentAssistantContent);
                yield break;
            }

            toolSources.MergeSources(response.GetSources());

            if (finalResponseRequired)
            {
                await ResetToolRuntimeStatusAsync(currentAssistantContent);

                var textOutput = response.GetTextOutput();
                if (!string.IsNullOrWhiteSpace(textOutput))
                    yield return new ContentStreamChunk(textOutput, [..toolSources]);
                else
                    yield return new ContentStreamChunk("The model did not return a final answer after completing the available tool calls.", [..toolSources]);

                yield break;
            }

            var functionCalls = response.GetFunctionCalls();
            if (functionCalls.Count == 0)
            {
                await ResetToolRuntimeStatusAsync(currentAssistantContent);

                var textOutput = response.GetTextOutput();
                if (!string.IsNullOrWhiteSpace(textOutput))
                    yield return new ContentStreamChunk(textOutput, [..toolSources]);
                else if (toolCallCount > 0)
                    yield return new ContentStreamChunk("The model completed the tool call but did not return a final answer.", [..toolSources]);

                yield break;
            }

            try
            {
                await ShowToolRuntimeStatusAsync(currentAssistantContent, functionCalls
                    .Select(x => runnableTools.FirstOrDefault(tool => tool.Definition.Function.Name.Equals(x.Name, StringComparison.Ordinal)).Implementation?.GetDisplayName() ?? x.Name));

                foreach (var outputItem in response.Output)
                    internalItems.Add(outputItem);

                foreach (var functionCall in functionCalls)
                {
                    var toolCallsUnavailableInstruction = ToolSelectionRules.GetToolCallsUnavailableInstruction(toolCallCount, toolResultCharacterCount);
                    if (toolCallsUnavailableInstruction is not null)
                    {
                        internalItems.Add(new ResponsesFunctionCallOutputItem
                        {
                            CallId = functionCall.CallId,
                            Output = toolCallsUnavailableInstruction,
                        });
                        continue;
                    }

                    toolCallCount++;
                    var (toolContent, trace, requiredProviderConfidence, sources) = await toolExecutor.ExecuteAsync(
                        functionCall.CallId,
                        functionCall.Name,
                        functionCall.Arguments,
                        runnableTools,
                        this,
                        toolCallCount,
                        token);
                    toolResultCharacterCount += toolContent.Length;

                    chatThread.RequireProviderConfidence(requiredProviderConfidence);
                    toolSources.MergeSources(sources);
                    currentAssistantContent?.ToolInvocations.Add(trace);
                    internalItems.Add(new ResponsesFunctionCallOutputItem
                    {
                        CallId = functionCall.CallId,
                        Output = toolContent,
                    });
                }

            }
            finally
            {
                await ResetToolRuntimeStatusAsync(currentAssistantContent);
            }
        }
    }

    private static async Task ResetToolRuntimeStatusAsync(ContentText? currentAssistantContent)
    {
        if (currentAssistantContent is null)
            return;

        currentAssistantContent.ToolRuntimeStatus = new();
        await currentAssistantContent.StreamingEvent();
    }

    private static async Task ShowToolRuntimeStatusAsync(ContentText? currentAssistantContent, IEnumerable<string> toolNames)
    {
        if (currentAssistantContent is null)
            return;

        currentAssistantContent.ToolRuntimeStatus = new ToolRuntimeStatus
        {
            IsRunning = true,
            ToolNames = toolNames.ToList(),
        };
        await currentAssistantContent.StreamingEvent();
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
            await MessageBus.INSTANCE.SendError(new(
                Icons.Material.Filled.Build,
                string.Format(TB("The tool calling request failed with status code {0}. See the logs for details."), (int)response.StatusCode)));
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

    /// <inheritdoc />
    public override async Task<ModelLoadResult> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var result = await this.LoadModels(SecretStoreType.LLM_PROVIDER, ["chatgpt-", "gpt-", "o1-", "o3-", "o4-"], token, apiKeyProvisional);
        return result with
        {
            Models =
            [
                ..result.Models.Where(model => !model.Id.Contains("image", StringComparison.OrdinalIgnoreCase) &&
                                               !model.Id.Contains("realtime", StringComparison.OrdinalIgnoreCase) &&
                                               !model.Id.Contains("audio", StringComparison.OrdinalIgnoreCase) &&
                                               !model.Id.Contains("tts", StringComparison.OrdinalIgnoreCase) &&
                                               !model.Id.Contains("transcribe", StringComparison.OrdinalIgnoreCase))
            ]
        };
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModels(SecretStoreType.IMAGE_PROVIDER, ["dall-e-", "gpt-image"], token, apiKeyProvisional);
    }
    
    /// <inheritdoc />
    public override Task<ModelLoadResult> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return this.LoadModels(SecretStoreType.EMBEDDING_PROVIDER, ["text-embedding-"], token, apiKeyProvisional);
    }
    
    /// <inheritdoc />
    public override async Task<ModelLoadResult> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        var result = await this.LoadModels(SecretStoreType.TRANSCRIPTION_PROVIDER, ["whisper-", "gpt-"], token, apiKeyProvisional);
        return result with
        {
            Models =
            [
                ..result.Models.Where(model => model.Id.StartsWith("whisper-", StringComparison.InvariantCultureIgnoreCase) ||
                                               model.Id.Contains("-transcribe", StringComparison.InvariantCultureIgnoreCase))
            ]
        };
    }
    
    #endregion

    private Task<ModelLoadResult> LoadModels(SecretStoreType storeType, string[] prefixes, CancellationToken token, string? apiKeyProvisional = null)
    {
        return this.LoadModelsResponse<ModelsResponse>(
            storeType,
            "models",
            modelResponse => modelResponse.Data.Where(model => prefixes.Any(prefix => model.Id.StartsWith(prefix, StringComparison.InvariantCulture))),
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
