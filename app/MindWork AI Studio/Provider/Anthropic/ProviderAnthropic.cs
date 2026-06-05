using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;
using AIStudio.Tools.ToolCallingSystem;

using Microsoft.Extensions.DependencyInjection;

namespace AIStudio.Provider.Anthropic;

public sealed class ProviderAnthropic() : BaseProvider(LLMProviders.ANTHROPIC, new Uri("https://api.anthropic.com/v1/"), ExternalHttpTrustPolicy.SYSTEM_TRUST_ONLY, LOGGER)
{
    private static readonly ILogger<ProviderAnthropic> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderAnthropic>();
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ProviderAnthropic).Namespace, nameof(ProviderAnthropic));

    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.ANTHROPIC.ToName();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "Anthropic";

    /// <inheritdoc />
    public override bool HasModelLoadingCapability => true;

    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        // Get the API key:
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this, SecretStoreType.LLM_PROVIDER);
        if(!requestedSecret.Success)
            yield break;
        
        // Parse the API parameters:
        var apiParameters = this.ParseAdditionalApiParameters("system", "tools");
        var maxTokens = 4_096;
        if (TryPopIntParameter(apiParameters, "max_tokens", out var parsedMaxTokens))
            maxTokens = parsedMaxTokens;

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
        
        var toolRegistry = Program.SERVICE_PROVIDER.GetService<ToolRegistry>();
        var toolExecutor = Program.SERVICE_PROVIDER.GetService<ToolExecutor>();
        var currentAssistantContent = chatThread.Blocks.LastOrDefault(x => x.Role is ChatRole.AI)?.Content as ContentText;
        currentAssistantContent?.ToolInvocations.Clear();
        var providerConfidence = this.Provider.GetConfidence(settingsManager).Level;
        IReadOnlyList<(ToolDefinition Definition, IToolImplementation Implementation)> runnableTools = toolRegistry is null
            ? []
            : await toolRegistry.GetRunnableToolsAsync(
                chatThread.RuntimeComponent,
                chatThread.RuntimeSelectedToolIds,
                this.Provider.GetModelCapabilities(chatModel),
                providerConfidence,
                settingsManager.IsToolSelectionVisible(chatThread.RuntimeComponent));

        if (toolExecutor is not null && runnableTools.Count > 0)
        {
            await foreach (var content in this.StreamWithLocalTools(
                               chatModel,
                               messages,
                               chatThread.PrepareSystemPrompt(settingsManager),
                               maxTokens,
                               apiParameters,
                               runnableTools,
                               toolExecutor,
                               currentAssistantContent,
                               requestedSecret,
                               providerConfidence,
                               token))
                yield return content;

            yield break;
        }

        // Prepare the Anthropic HTTP chat request:
        var chatRequest = JsonSerializer.Serialize(new ChatRequest
        {
            Model = chatModel.Id,
            
            // Build the messages:
            Messages = [..messages],
            
            System = chatThread.PrepareSystemPrompt(settingsManager),
            MaxTokens = maxTokens,
            
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

    private async IAsyncEnumerable<ContentStreamChunk> StreamWithLocalTools(
        Model chatModel,
        IList<IMessageBase> baseMessages,
        string systemPrompt,
        int maxTokens,
        IDictionary<string, object> apiParameters,
        IReadOnlyList<(ToolDefinition Definition, IToolImplementation Implementation)> runnableTools,
        ToolExecutor toolExecutor,
        ContentText? currentAssistantContent,
        RequestedSecret requestedSecret,
        ConfidenceLevel providerConfidence,
        [EnumeratorCancellation] CancellationToken token)
    {
        var providerTools = runnableTools
            .Select(x => (object)new AnthropicTool
            {
                Name = x.Definition.Function.Name,
                Description = x.Definition.Function.Description,
                Strict = x.Definition.Function.Strict,
                InputSchema = NormalizeInputSchemaForAnthropic(x.Definition.Function.Parameters),
            })
            .ToList();
        var internalMessages = new List<IMessageBase>();
        var toolCallCount = 0;
        const int MAX_TOOL_CALLS = 30;

        while (true)
        {
            var requestDto = new ChatRequest
            {
                Model = chatModel.Id,
                Messages = [..baseMessages, ..internalMessages],
                MaxTokens = maxTokens,
                Stream = false,
                System = systemPrompt,
                Tools = providerTools,
                AdditionalApiParameters = apiParameters,
            };
            var response = await this.ExecuteMessagesRequest(requestDto, requestedSecret, token);
            if (response is null)
            {
                if (currentAssistantContent is not null)
                {
                    currentAssistantContent.ToolRuntimeStatus = new();
                    await currentAssistantContent.StreamingEvent();
                }

                yield break;
            }

            var textOutput = response.GetTextOutput();
            var toolUses = response.GetToolUses();
            if (toolUses.Count > 0 && !string.IsNullOrWhiteSpace(textOutput))
                yield return new ContentStreamChunk(textOutput, []);

            if (toolUses.Count == 0)
            {
                if (currentAssistantContent is not null)
                {
                    currentAssistantContent.ToolRuntimeStatus = new();
                    await currentAssistantContent.StreamingEvent();
                }

                if (!string.IsNullOrWhiteSpace(textOutput))
                    yield return new ContentStreamChunk(textOutput, []);
                
                if (!response.HasFinalStopReason())
                {
                    yield return new ContentStreamChunk($"The model stopped with reason '{response.StopReason}' before returning a final answer.", []);
                    yield break;
                }

                else if (toolCallCount > 0)
                    yield return new ContentStreamChunk("The model completed the tool call but did not return a final answer.", []);

                yield break;
            }

            if (currentAssistantContent is not null)
            {
                currentAssistantContent.ToolRuntimeStatus = new ToolRuntimeStatus
                {
                    IsRunning = true,
                    ToolNames = toolUses
                        .Select(x => runnableTools.FirstOrDefault(tool => tool.Definition.Function.Name.Equals(x.Name, StringComparison.Ordinal)).Implementation?.GetDisplayName() ?? x.Name)
                        .ToList(),
                };
                await currentAssistantContent.StreamingEvent();
            }

            internalMessages.Add(new AnthropicMessage(response.Content, "assistant"));
            var toolResults = new List<AnthropicToolResultContent>();
            foreach (var toolUse in toolUses)
            {
                toolCallCount++;
                if (toolCallCount > MAX_TOOL_CALLS)
                {
                    var limitMessage = $"Tool calling stopped because the maximum of {MAX_TOOL_CALLS} tool calls was reached.";
                    currentAssistantContent?.ToolInvocations.Add(new ToolInvocationTrace
                    {
                        Order = toolCallCount,
                        ToolId = toolUse.Name,
                        ToolName = toolUse.Name,
                        ToolCallId = toolUse.Id,
                        Status = ToolInvocationTraceStatus.BLOCKED,
                        StatusMessage = limitMessage,
                        Result = limitMessage,
                    });

                    if (currentAssistantContent is not null)
                    {
                        currentAssistantContent.ToolRuntimeStatus = new();
                        await currentAssistantContent.StreamingEvent();
                    }

                    yield return new ContentStreamChunk(limitMessage, []);
                    yield break;
                }

                var (toolContent, trace) = await toolExecutor.ExecuteAsync(
                    toolUse.Id,
                    toolUse.Name,
                    toolUse.Arguments,
                    runnableTools,
                    providerConfidence,
                    toolCallCount,
                    token);

                currentAssistantContent?.ToolInvocations.Add(trace);
                toolResults.Add(new AnthropicToolResultContent
                {
                    ToolUseId = toolUse.Id,
                    Content = toolContent,
                });
            }

            internalMessages.Add(new AnthropicToolResultMessage(toolResults));

            if (currentAssistantContent is not null)
                await currentAssistantContent.StreamingEvent();
        }
    }

    private async Task<AnthropicResponse?> ExecuteMessagesRequest(ChatRequest requestDto, RequestedSecret requestedSecret, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "messages");
        request.Headers.Add("x-api-key", await requestedSecret.Secret.Decrypt(ENCRYPTION));
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(JsonSerializer.Serialize(requestDto, JSON_SERIALIZER_OPTIONS), Encoding.UTF8, "application/json");

        using var response = await this.HttpClient.SendAsync(request, token);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(token);
            LOGGER.LogError("Tool calling Anthropic Messages API request failed with status code {ResponseStatusCode} and body: '{ResponseBody}'.", response.StatusCode, responseBody);
            await MessageBus.INSTANCE.SendError(new(
                Icons.Material.Filled.Build,
                string.Format(TB("The tool calling request failed with status code {0}. See the logs for details."), (int)response.StatusCode)));
            return null;
        }

        return await response.Content.ReadFromJsonAsync<AnthropicResponse>(JSON_SERIALIZER_OPTIONS, token);
    }

    #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    /// <inheritdoc />
    public override async IAsyncEnumerable<ImageURL> StreamImageCompletion(Model imageModel, string promptPositive, string promptNegative = FilterOperator.String.Empty, ImageURL referenceImageURL = default, [EnumeratorCancellation] CancellationToken token = default)
    {
        yield break;
    }
    #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    
    /// <inheritdoc />
    public override Task<TranscriptionResult> TranscribeAudioAsync(Model transcriptionModel, string audioFilePath, SettingsManager settingsManager, CancellationToken token = default)
    {
        return Task.FromResult(TranscriptionResult.Failure());
    }
    
    /// <inhertidoc />
    public override Task<IReadOnlyList<IReadOnlyList<float>>> EmbedTextAsync(Model embeddingModel, SettingsManager settingsManager, CancellationToken token = default, params List<string> texts)
    {
        return Task.FromResult<IReadOnlyList<IReadOnlyList<float>>>([]);
    }

    /// <inheritdoc />
    public override async Task<ModelLoadResult> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default)
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
        
        var result = await this.LoadModels(SecretStoreType.LLM_PROVIDER, token, apiKeyProvisional);
        return result with
        {
            Models = [..result.Models.Concat(additionalModels).OrderBy(x => x.Id)]
        };
    }

    /// <inheritdoc />
    public override Task<ModelLoadResult> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(ModelLoadResult.FromModels([]));
    }
    
    /// <inheritdoc />
    public override Task<ModelLoadResult> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(ModelLoadResult.FromModels([]));
    }
    
    /// <inheritdoc />
    public override Task<ModelLoadResult> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default)
    {
        return Task.FromResult(ModelLoadResult.FromModels([]));
    }
    
    #endregion
    
    private Task<ModelLoadResult> LoadModels(SecretStoreType storeType, CancellationToken token, string? apiKeyProvisional = null)
    {
        return this.LoadModelsResponse<ModelsResponse>(
            storeType,
            "models?limit=100",
            modelResponse => modelResponse.Data,
            token,
            apiKeyProvisional,
            failureReasonSelector: (response, _) => response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => ModelLoadFailureReason.INVALID_OR_MISSING_API_KEY,
                System.Net.HttpStatusCode.Forbidden => ModelLoadFailureReason.AUTHENTICATION_OR_PERMISSION_ERROR,
                System.Net.HttpStatusCode.TooManyRequests => ModelLoadFailureReason.TOO_MANY_REQUESTS,
                _ => ModelLoadFailureReason.PROVIDER_UNAVAILABLE,
            },
            requestConfigurator: (request, secretKey) =>
            {
                request.Headers.Add("x-api-key", secretKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
            },
            jsonSerializerOptions: JSON_SERIALIZER_OPTIONS);
    }

    private static JsonElement NormalizeInputSchemaForAnthropic(JsonElement schema)
    {
        JsonNode? root = JsonNode.Parse(schema.GetRawText());
        if (root is JsonObject rootObject)
            NormalizeSchemaNode(rootObject);

        return JsonSerializer.SerializeToElement(root);
    }

    private static void NormalizeSchemaNode(JsonObject schemaObject)
    {
        var allowsNull = DeclaresNullType(schemaObject["type"]);
        if (allowsNull && schemaObject["enum"] is JsonArray enumArray)
        {
            for (var i = enumArray.Count - 1; i >= 0; i--)
            {
                if (enumArray[i]?.GetValueKind() is JsonValueKind.Null)
                    enumArray.RemoveAt(i);
            }
        }

        if (schemaObject["properties"] is JsonObject propertiesObject)
        {
            foreach (var property in propertiesObject)
            {
                if (property.Value is JsonObject childObject)
                    NormalizeSchemaNode(childObject);
            }
        }

        if (schemaObject["items"] is JsonObject itemsObject)
            NormalizeSchemaNode(itemsObject);

        if (schemaObject["anyOf"] is JsonArray anyOfArray)
        {
            foreach (var entry in anyOfArray)
            {
                if (entry is JsonObject childObject)
                    NormalizeSchemaNode(childObject);
            }
        }

        if (schemaObject["oneOf"] is JsonArray oneOfArray)
        {
            foreach (var entry in oneOfArray)
            {
                if (entry is JsonObject childObject)
                    NormalizeSchemaNode(childObject);
            }
        }

        if (schemaObject["allOf"] is JsonArray allOfArray)
        {
            foreach (var entry in allOfArray)
            {
                if (entry is JsonObject childObject)
                    NormalizeSchemaNode(childObject);
            }
        }
    }

    private static bool DeclaresNullType(JsonNode? typeNode) => typeNode switch
    {
        JsonValue value when value.TryGetValue<string>(out var typeName) => typeName.Equals("null", StringComparison.Ordinal),
        JsonArray array => array.Any(entry => entry is JsonValue value && value.TryGetValue<string>(out var typeName) && typeName.Equals("null", StringComparison.Ordinal)),
        _ => false,
    };
}
