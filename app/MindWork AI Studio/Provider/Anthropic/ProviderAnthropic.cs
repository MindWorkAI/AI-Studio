using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;
using AIStudio.Tools.Rust;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.Harness;

namespace AIStudio.Provider.Anthropic;

public sealed class ProviderAnthropic() : BaseProvider(LLMProviders.ANTHROPIC, new Uri("https://api.anthropic.com/v1/"), ExternalHttpTrustPolicy.SYSTEM_TRUST_ONLY, LOGGER)
{
    private static readonly ILogger<ProviderAnthropic> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderAnthropic>();
    #region Implementation of IProvider

    /// <inheritdoc />
    public override string Id => LLMProviders.ANTHROPIC.ToSecretId();

    /// <inheritdoc />
    public override string InstanceName { get; set; } = "Anthropic";

    /// <inheritdoc />
    public override bool HasModelLoadingCapability => true;

    /// <inheritdoc />
    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        // Get the API key:
        var requestedSecret = await Program.RUST_SERVICE.GetAPIKey(this, SecretStoreType.LLM_PROVIDER);
        if(!requestedSecret.Success)
            yield break;
        
        // Parse the API parameters:
        var apiParameters = this.ParseAdditionalApiParameters("system");
        var maxTokens = 4_096;
        if (TryPopIntParameter(apiParameters, "max_tokens", out var parsedMaxTokens))
            maxTokens = parsedMaxTokens;

        // Build the list of messages:
        var messages = await chatThread.Blocks.BuildMessagesAsync(
            this.CreateSettingsProvider(chatModel),

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

        //
        // Prepare the tools we want to use. When the model may call one, the conversation runs
        // through the harness instead of going straight to the streaming path below. It streams
        // there as well, round by round -- what the harness adds is the tools in between.
        //
        var toolRegistry = Program.SERVICE_PROVIDER.GetService<ToolRegistry>();
        var toolExecutor = Program.SERVICE_PROVIDER.GetService<ToolExecutor>();
        var currentAssistantContent = chatThread.Blocks.LastOrDefault(x => x.Role is ChatRole.AI)?.Content as ContentText;
        currentAssistantContent?.BeginToolRun();

        var providerSettings = this.CreateSettingsProvider(chatModel);
        var runnableTools = toolRegistry is null
            ? []
            : await toolRegistry.GetRunnableToolsAsync(providerSettings, chatThread.RuntimeComponent, chatThread.RuntimeSelectedToolIds,
                this.Provider.GetConfidence(settingsManager).Level, chatThread.MayRunTools(settingsManager));

        var systemPrompt = chatThread.PrepareSystemPrompt(settingsManager, runnableTools.Select(x => x.Definition));
        if (toolExecutor is not null && runnableTools.Count > 0)
        {
            var adapter = new AnthropicToolCallingAdapter(chatModel, [..messages], systemPrompt, maxTokens, apiParameters, runnableTools,
                (requestDto, requestToken) => this.StreamMessagesRequest(requestDto, requestedSecret, requestToken));

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

        // Prepare the Anthropic HTTP chat request:
        var chatRequest = JsonSerializer.Serialize(new ChatRequest
        {
            Model = chatModel.Id,

            // Build the messages:
            Messages = [..messages],

            System = systemPrompt,
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
            request.Headers.Add("x-api-key", await requestedSecret.Secret.Decrypt(Program.ENCRYPTION));

            // Set the Anthropic version:
            request.Headers.Add("anthropic-version", "2023-06-01");

            // Set the content:
            request.Content = new StringContent(chatRequest, Encoding.UTF8, "application/json");
            return request;
        }
        
        await foreach (var content in this.StreamChatCompletionInternal<ResponseStreamLine, NoChatCompletionAnnotationStreamLine>("Anthropic", RequestBuilder, token))
            yield return content;
    }

    /// <summary>
    /// Runs one round of a tool calling conversation against the messages API.
    /// </summary>
    /// <remarks>
    /// Nothing but the HTTP request is done here. The retries, the timeouts, and the error
    /// classification come from the shared stream reader, which the tool rounds used to go
    /// without; reading the events is the adapter's business.
    /// </remarks>
    private IAsyncEnumerable<ServerSentEvent> StreamMessagesRequest(ChatRequest requestDto, RequestedSecret requestedSecret, CancellationToken token)
    {
        async Task<HttpRequestMessage> RequestBuilder()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "messages");
            request.Headers.Add("x-api-key", await requestedSecret.Secret.Decrypt(Program.ENCRYPTION));
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Content = new StringContent(JsonSerializer.Serialize(requestDto, JSON_SERIALIZER_OPTIONS), Encoding.UTF8, "application/json");
            return request;
        }

        return this.ReadServerSentEventsAsync("Anthropic", "messages call", RequestBuilder, token);
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
        throw this.CreateEmbeddingsNotSupportedException();
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
        
        var result = await this.LoadModels(SecretStoreType.LLM_PROVIDER, apiKeyProvisional, token);
        return result with
        {
            //
            // The API is the authority: when it reports a model we also keep as a fallback above,
            // its entry comes first and the fallback is dropped. What it reports is asked about
            // first, though -- the route says nothing about what a model is made for, and Claude
            // has not always been only something to talk to. The six above skip that question
            // because they are not a catalog: every one of them was picked by hand.
            //
            Models = [..result.Models.Where(model => model.IsChatModel(this.Provider)).Concat(additionalModels).DistinctBy(x => x.Id).OrderBy(x => x.Id)]
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

    private Task<ModelLoadResult> LoadModels(SecretStoreType storeType, string? apiKeyProvisional, CancellationToken token)
    {
        return this.LoadModelsResponse<ModelsResponse>(
            storeType,
            "models?limit=100",
            modelResponse => modelResponse.Data,
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
            jsonSerializerOptions: JSON_SERIALIZER_OPTIONS, token: token);
    }
}