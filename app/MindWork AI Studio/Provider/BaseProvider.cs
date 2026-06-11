using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using AIStudio.Chat;
using AIStudio.Provider.Anthropic;
using AIStudio.Provider.OpenAI;
using AIStudio.Provider.SelfHosted;
using AIStudio.Settings;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.MIME;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.Extensions.DependencyInjection;

using Host = AIStudio.Provider.SelfHosted.Host;

namespace AIStudio.Provider;

/// <summary>
/// The base class for all providers.
/// </summary>
public abstract class BaseProvider : IProvider, ISecretId
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(BaseProvider).Namespace, nameof(BaseProvider));
    
    /// <summary>
    /// The HTTP client to use it for all requests.
    /// </summary>
    protected readonly HttpClient HttpClient;
    
    /// <summary>
    /// The logger to use.
    /// </summary>
    private readonly ILogger logger;

    static BaseProvider()
    {
        RUST_SERVICE = Program.RUST_SERVICE;
        ENCRYPTION = Program.ENCRYPTION;
    }

    protected static readonly RustService RUST_SERVICE;
    
    protected static readonly Encryption ENCRYPTION;
    
    protected static readonly JsonSerializerOptions JSON_SERIALIZER_OPTIONS = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
            new AnnotationConverter(),
            new MessageBaseConverter(),
            new SubContentConverter(),
            new SubContentImageSourceConverter(),
            new SubContentImageUrlConverter(),
        },
        AllowTrailingCommas = false
    };

    /// <summary>
    /// Constructor for the base provider.
    /// </summary>
    /// <param name="provider">The provider enum value.</param>
    /// <param name="baseUri">The base URI for the provider.</param>
    /// <param name="trustPolicy">The trust policy for external HTTPS requests to this provider.</param>
    /// <param name="logger">The logger to use.</param>
    protected BaseProvider(LLMProviders provider, Uri baseUri, ExternalHttpTrustPolicy trustPolicy, ILogger logger)
    {
        this.logger = logger;
        this.Provider = provider;
        this.BaseUri = baseUri;
        this.HttpClient = ExternalHttpClientTimeout.CreateHttpClient(baseUri, trustPolicy);
    }
    
    #region Handling of IProvider, which all providers must implement
    
    /// <inheritdoc />
    public LLMProviders Provider { get; }

    /// <summary>
    /// The base URI for all relative provider requests.
    /// </summary>
    public Uri BaseUri { get; }
    
    /// <inheritdoc />
    public abstract string Id { get; }
    
    /// <inheritdoc />
    public abstract string InstanceName { get; set; }

    /// <inheritdoc />
    public string AdditionalJsonApiParameters { get; init; } = string.Empty;

    /// <inheritdoc />
    public abstract bool HasModelLoadingCapability { get; }

    /// <inheritdoc />
    public abstract IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, CancellationToken token = default);
    
    /// <inheritdoc />
    public abstract IAsyncEnumerable<ImageURL> StreamImageCompletion(Model imageModel, string promptPositive, string promptNegative = FilterOperator.String.Empty, ImageURL referenceImageURL = default, CancellationToken token = default);
    
    /// <inheritdoc />
    public abstract Task<TranscriptionResult> TranscribeAudioAsync(Model transcriptionModel, string audioFilePath, SettingsManager settingsManager, CancellationToken token = default);
    
    /// <inheritdoc />
    public abstract Task<IReadOnlyList<IReadOnlyList<float>>> EmbedTextAsync(Model embeddingModel, SettingsManager settingsManager, CancellationToken token = default, params List<string> texts);
    
    /// <inheritdoc />
    public abstract Task<ModelLoadResult> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default);
    
    /// <inheritdoc />
    public abstract Task<ModelLoadResult> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default);
    
    /// <inheritdoc />
    public abstract Task<ModelLoadResult> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default);

    /// <inheritdoc />
    public abstract Task<ModelLoadResult> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default);
    
    #endregion
    
    /// <summary>
    /// Whether this provider was imported from an enterprise configuration plugin.
    /// </summary>
    public bool IsEnterpriseConfiguration { get; init; }

    #region Implementation of ISecretId

    public string SecretId => this.IsEnterpriseConfiguration ? $"{ISecretId.ENTERPRISE_KEY_PREFIX}::{this.Id}" : this.Id;

    public string SecretName => this.InstanceName;

    #endregion

    protected static ModelLoadResult SuccessfulModelLoadResult(IEnumerable<Model> models) => ModelLoadResult.FromModels(models);

    protected static ModelLoadResult FailedModelLoadResult(ModelLoadFailureReason failureReason, string? technicalDetails = null) => ModelLoadResult.Failure(failureReason, technicalDetails);

    protected bool IsTimeoutException(Exception exception, CancellationToken token = default)
    {
        if (token.IsCancellationRequested)
            return false;

        return ExternalHttpClientTimeout.IsTimeoutException(exception, token);
    }

    protected Task SendTimeoutError(string action) => MessageBus.INSTANCE.SendError(new(
        Icons.Material.Filled.HourglassTop,
        string.Format(
            TB("The request to the LLM provider '{0}' (type={1}) timed out after {2} while {3}. Please try again or check whether the provider is still responding."),
            this.InstanceName,
            this.Provider,
            ExternalHttpClientTimeout.GetTimeoutDescription(),
            action)));

    protected async Task<string?> GetModelLoadingSecretKey(SecretStoreType storeType, string? apiKeyProvisional = null, bool isTryingSecret = false) => apiKeyProvisional switch
    {
        not null => apiKeyProvisional,
        _ => await RUST_SERVICE.GetAPIKey(this, storeType, isTrying: isTryingSecret) switch
        {
            { Success: true } result => await result.Secret.Decrypt(ENCRYPTION),
            _ => null,
        }
    };

    protected static ModelLoadFailureReason GetDefaultModelLoadFailureReason(HttpResponseMessage response) => response.StatusCode switch
    {
        HttpStatusCode.Unauthorized => ModelLoadFailureReason.INVALID_OR_MISSING_API_KEY,
        HttpStatusCode.Forbidden => ModelLoadFailureReason.AUTHENTICATION_OR_PERMISSION_ERROR,
        HttpStatusCode.TooManyRequests => ModelLoadFailureReason.TOO_MANY_REQUESTS,
        
        _ => ModelLoadFailureReason.PROVIDER_UNAVAILABLE,
    };

    protected ModelLoadFailureReason GetModelLoadFailureReason(HttpResponseMessage response, string responseBody) => this.ClassifyProviderRequestFailure(response.StatusCode, responseBody) switch
    {
        ProviderRequestFailureReason.INSUFFICIENT_QUOTA => ModelLoadFailureReason.INSUFFICIENT_QUOTA,
        ProviderRequestFailureReason.TOO_MANY_REQUESTS => ModelLoadFailureReason.TOO_MANY_REQUESTS,
        _ => GetDefaultModelLoadFailureReason(response),
    };

    protected async Task<ModelLoadResult> LoadModelsResponse<TResponse>(
        SecretStoreType storeType,
        string requestPath,
        Func<TResponse, IEnumerable<Model>> modelFactory,
        CancellationToken token,
        string? apiKeyProvisional = null,
        Func<HttpResponseMessage, string, ModelLoadFailureReason>? failureReasonSelector = null,
        Action<HttpRequestMessage, string>? requestConfigurator = null,
        JsonSerializerOptions? jsonSerializerOptions = null,
        bool isTryingSecret = false)
    {
        var secretKey = await this.GetModelLoadingSecretKey(storeType, apiKeyProvisional, isTryingSecret);
        if (string.IsNullOrWhiteSpace(secretKey) && !isTryingSecret)
            return FailedModelLoadResult(ModelLoadFailureReason.INVALID_OR_MISSING_API_KEY, "No API key available for model loading.");

        using var request = new HttpRequestMessage(HttpMethod.Get, requestPath);
        if (requestConfigurator is not null)
            requestConfigurator(request, secretKey ?? string.Empty);
        else if (!string.IsNullOrWhiteSpace(secretKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);

        try
        {
            using var response = await this.HttpClient.SendAsync(request, token);
            var responseBody = await response.Content.ReadAsStringAsync(token);
            if (!response.IsSuccessStatusCode)
            {
                var failureReason = failureReasonSelector?.Invoke(response, responseBody) ?? this.GetModelLoadFailureReason(response, responseBody);
                this.logger.LogError("Model loading request failed with status code {ResponseStatusCode} (message = '{ResponseReasonPhrase}', error body = '{ErrorBody}').", response.StatusCode, response.ReasonPhrase, responseBody);
                return FailedModelLoadResult(failureReason, $"Status={(int)response.StatusCode} {response.ReasonPhrase}; Body='{responseBody}'");
            }

            try
            {
                var parsedResponse = JsonSerializer.Deserialize<TResponse>(responseBody, jsonSerializerOptions ?? JSON_SERIALIZER_OPTIONS);
                if (parsedResponse is null)
                    return FailedModelLoadResult(ModelLoadFailureReason.INVALID_RESPONSE, "Model list response could not be deserialized.");

                return SuccessfulModelLoadResult(modelFactory(parsedResponse));
            }
            catch (Exception e)
            {
                return FailedModelLoadResult(ModelLoadFailureReason.INVALID_RESPONSE, e.Message);
            }
        }
        catch (Exception e) when (this.IsTimeoutException(e, token))
        {
            await this.SendTimeoutError("loading the available models");
            this.logger.LogError(e, "Timed out while loading models from provider '{ProviderInstanceName}' (provider={ProviderType}).", this.InstanceName, this.Provider);
            return FailedModelLoadResult(ModelLoadFailureReason.PROVIDER_UNAVAILABLE, e.Message);
        }
    }

    protected virtual string GetProviderRequestFailureUserMessage(ProviderRequestFailureReason failureReason) => failureReason switch
    {
        ProviderRequestFailureReason.TOO_MANY_REQUESTS => TB("The provider rejected the request because too many requests were sent. Please wait a moment and try again."),
        _ => string.Empty,
    };

    protected virtual ProviderRequestFailureReason ClassifyProviderRequestFailure(HttpStatusCode statusCode, string responseBody)
    {
        if (statusCode is not HttpStatusCode.TooManyRequests)
            return ProviderRequestFailureReason.NONE;

        return ProviderRequestFailureReason.TOO_MANY_REQUESTS;
    }

    protected virtual ProviderRequestFailureReason ClassifyProviderRequestFailure(string? errorCode, string? errorType, string? errorMessage, string responseBody)
    {
        if (IsTooManyRequestsError(errorCode) || IsTooManyRequestsError(errorType) || IsTooManyRequestsError(errorMessage))
            return ProviderRequestFailureReason.TOO_MANY_REQUESTS;

        return ProviderRequestFailureReason.NONE;
    }

    private static bool IsTooManyRequestsError(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Equals("rate_limit_exceeded", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("too_many_requests", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("too_many_request", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("too many requests", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("rate_limit", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("throttl", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryCreateProviderRequestExceptionFromStreamLine(string providerName, string line, out ProviderRequestException exception)
    {
        exception = new();

        if (!line.StartsWith("data: ", StringComparison.InvariantCulture))
            return false;

        var jsonData = line[6..].Trim();
        if (string.IsNullOrWhiteSpace(jsonData) || jsonData is "[DONE]")
            return false;

        try
        {
            using var document = JsonDocument.Parse(jsonData);
            var root = document.RootElement;
            if (!IsProviderStreamFailure(root))
                return false;

            var eventType = TryGetString(root, "type");
            TryGetProviderStreamError(root, out var errorCode, out var errorType, out var errorMessage);
            var failureReason = this.ClassifyProviderRequestFailure(errorCode, errorType, errorMessage, jsonData);
            var userMessage = this.GetProviderRequestFailureUserMessage(failureReason);
            if (string.IsNullOrWhiteSpace(userMessage))
            {
                userMessage = string.IsNullOrWhiteSpace(errorMessage)
                    ? string.Format(TB("The provider '{0}' reported an error while streaming the response."), this.InstanceName)
                    : string.Format(TB("The provider '{0}' reported an error: {1}"), this.InstanceName, errorMessage);
            }

            this.logger.LogError("The {ProviderName} stream returned an error for provider '{ProviderInstanceName}' (provider={ProviderType}). EventType={StreamEventType}, ErrorCode={ErrorCode}, ErrorType={ErrorType}, ErrorMessage='{ErrorMessage}', Body='{ErrorBody}'", providerName, this.InstanceName, this.Provider, eventType, errorCode, errorType, errorMessage, jsonData);
            exception = new ProviderRequestException(failureReason, userMessage, responseBody: jsonData);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsProviderStreamFailure(JsonElement root)
    {
        var eventType = TryGetString(root, "type");
        if (eventType is not null && (
                eventType.Equals("error", StringComparison.OrdinalIgnoreCase) ||
                eventType.Equals("response.error", StringComparison.OrdinalIgnoreCase) ||
                eventType.Equals("response.failed", StringComparison.OrdinalIgnoreCase)))
            return true;

        if (HasObjectProperty(root, "error"))
            return true;

        if (IsTooManyRequestsError(TryGetString(root, "code")) ||
            IsTooManyRequestsError(TryGetString(root, "type")) ||
            IsTooManyRequestsError(TryGetString(root, "message")))
            return true;

        if (TryGetString(root, "message") is not null &&
            (TryGetString(root, "code") is not null || TryGetString(root, "type") is not null) &&
            !root.TryGetProperty("choices", out _) &&
            !root.TryGetProperty("delta", out _))
            return true;

        if (!root.TryGetProperty("response", out var responseElement) || responseElement.ValueKind is not JsonValueKind.Object)
            return false;

        if (HasObjectProperty(responseElement, "error"))
            return true;

        var responseStatus = TryGetString(responseElement, "status");
        return responseStatus is not null && responseStatus.Equals("failed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasObjectProperty(JsonElement element, string propertyName)
    {
        return element.ValueKind is JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var propertyElement) &&
               propertyElement.ValueKind is JsonValueKind.Object;
    }

    private static void TryGetProviderStreamError(JsonElement root, out string? errorCode, out string? errorType, out string? errorMessage)
    {
        errorCode = null;
        errorType = null;
        errorMessage = null;

        if (TryGetErrorElement(root, out var errorElement))
        {
            errorCode = TryGetString(errorElement, "code");
            errorType = TryGetString(errorElement, "type");
            errorMessage = TryGetString(errorElement, "message");
            return;
        }

        errorCode = TryGetString(root, "code");
        errorType = TryGetString(root, "type");
        errorMessage = TryGetString(root, "message");
    }

    private static bool TryGetErrorElement(JsonElement root, out JsonElement errorElement)
    {
        if (root.ValueKind is JsonValueKind.Object &&
            root.TryGetProperty("error", out errorElement) &&
            errorElement.ValueKind is JsonValueKind.Object)
            return true;

        if (root.ValueKind is JsonValueKind.Object &&
            root.TryGetProperty("response", out var responseElement) &&
            responseElement.ValueKind is JsonValueKind.Object &&
            responseElement.TryGetProperty("error", out errorElement) &&
            errorElement.ValueKind is JsonValueKind.Object)
            return true;

        errorElement = default;
        return false;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind is not JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var propertyElement) ||
            propertyElement.ValueKind is not JsonValueKind.String)
            return null;

        return propertyElement.GetString();
    }
    
    /// <summary>
    /// Sends a request and handles rate limiting by exponential backoff.
    /// </summary>
    /// <param name="requestBuilder">A function that builds the request.</param>
    /// <param name="userCancellationToken">The user cancellation token.</param>
    /// <param name="requestCancellationToken">The token to use for the HTTP request.</param>
    /// <returns>The status object of the request.</returns>
    private async Task<HttpRateLimitedStreamResult> SendRequest(Func<Task<HttpRequestMessage>> requestBuilder, CancellationToken userCancellationToken = default, CancellationToken requestCancellationToken = default)
    {
        const int MAX_RETRIES = 6;
        const double RETRY_DELAY_SECONDS = 4;
        var effectiveCancellationToken = requestCancellationToken.CanBeCanceled ? requestCancellationToken : userCancellationToken;
        
        var retry = 0;
        var response = default(HttpResponseMessage);
        var errorMessage = string.Empty;
        var lastProviderRequestFailure = ProviderRequestFailureReason.NONE;
        HttpStatusCode? lastResponseStatusCode = null;
        var lastResponseReasonPhrase = string.Empty;
        var lastErrorBody = string.Empty;
        while (retry++ < MAX_RETRIES)
        {
            using var request = await requestBuilder();
            
            //
            // Send the request with the ResponseHeadersRead option.
            // This allows us to read the stream as soon as the headers are received.
            // This is important because we want to stream the responses.
            //
            // Please notice: We do not dispose the response here. The caller is responsible
            // for disposing the response object. This is important because the response
            // object is used to read the stream.
            HttpResponseMessage nextResponse;
            try
            {
                nextResponse = await this.HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, effectiveCancellationToken);
            }
            catch (Exception e) when (this.IsTimeoutException(e, userCancellationToken))
            {
                await this.SendTimeoutError("waiting for the chat response");
                this.logger.LogError(e, "Timed out while sending a streaming request to provider '{ProviderInstanceName}' (provider={ProviderType}).", this.InstanceName, this.Provider);
                return new HttpRateLimitedStreamResult(false, true, e.Message, response);
            }

            if (nextResponse.IsSuccessStatusCode)
            {
                response = nextResponse;
                errorMessage = string.Empty;
                lastProviderRequestFailure = ProviderRequestFailureReason.NONE;
                break;
            }

            var errorBody = await nextResponse.Content.ReadAsStringAsync(effectiveCancellationToken);
            lastResponseStatusCode = nextResponse.StatusCode;
            lastResponseReasonPhrase = nextResponse.ReasonPhrase ?? string.Empty;
            lastErrorBody = errorBody;
            var providerRequestFailure = this.ClassifyProviderRequestFailure(nextResponse.StatusCode, errorBody);
            lastProviderRequestFailure = providerRequestFailure;
            if (providerRequestFailure is ProviderRequestFailureReason.INSUFFICIENT_QUOTA)
            {
                var userMessage = this.GetProviderRequestFailureUserMessage(providerRequestFailure);
                this.logger.LogError("Failed request with status code {ResponseStatusCode} (message = '{ResponseReasonPhrase}', error body = '{ErrorBody}').", nextResponse.StatusCode, nextResponse.ReasonPhrase, errorBody);
                throw new ProviderRequestException(providerRequestFailure, userMessage, nextResponse.StatusCode, nextResponse.ReasonPhrase ?? string.Empty, errorBody);
            }

            if (nextResponse.StatusCode is HttpStatusCode.Forbidden)
            {
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Block, string.Format(TB("We tried to communicate with the LLM provider '{0}' (type={1}). You might not be able to use this provider from your location. The provider message is: '{2}'"), this.InstanceName, this.Provider, nextResponse.ReasonPhrase)));
                this.logger.LogError("Failed request with status code {ResponseStatusCode} (message = '{ResponseReasonPhrase}', error body = '{ErrorBody}').", nextResponse.StatusCode, nextResponse.ReasonPhrase, errorBody);
                errorMessage = nextResponse.ReasonPhrase;
                break;
            }
            
            if(nextResponse.StatusCode is HttpStatusCode.BadRequest)
            {
                // Check if the error body contains "context" and "token" (case-insensitive),
                // which indicates that the context window is likely exceeded:
                if(errorBody.Contains("context", StringComparison.InvariantCultureIgnoreCase) &&
                   errorBody.Contains("token", StringComparison.InvariantCultureIgnoreCase))
                {
                    await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.CloudOff, string.Format(TB("We tried to communicate with the LLM provider '{0}' (type={1}). The data of the chat, including all file attachments, is probably too large for the selected model and provider. The provider message is: '{2}'"), this.InstanceName, this.Provider, nextResponse.ReasonPhrase)));
                }
                else
                {
                    await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.CloudOff, string.Format(TB("We tried to communicate with the LLM provider '{0}' (type={1}). The required message format might be changed. The provider message is: '{2}'"), this.InstanceName, this.Provider, nextResponse.ReasonPhrase)));
                }

                this.logger.LogError("Failed request with status code {ResponseStatusCode} (message = '{ResponseReasonPhrase}', error body = '{ErrorBody}').", nextResponse.StatusCode, nextResponse.ReasonPhrase, errorBody);
                errorMessage = nextResponse.ReasonPhrase;
                break;
            }
            
            if(nextResponse.StatusCode is HttpStatusCode.NotFound)
            {
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.CloudOff, string.Format(TB("We tried to communicate with the LLM provider '{0}' (type={1}). Something was not found. The provider message is: '{2}'"), this.InstanceName, this.Provider, nextResponse.ReasonPhrase)));
                this.logger.LogError("Failed request with status code {ResponseStatusCode} (message = '{ResponseReasonPhrase}', error body = '{ErrorBody}').", nextResponse.StatusCode, nextResponse.ReasonPhrase, errorBody);
                errorMessage = nextResponse.ReasonPhrase;
                break;
            }
            
            if(nextResponse.StatusCode is HttpStatusCode.Unauthorized)
            {
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Key, string.Format(TB("We tried to communicate with the LLM provider '{0}' (type={1}). The API key might be invalid. The provider message is: '{2}'"), this.InstanceName, this.Provider, nextResponse.ReasonPhrase)));
                this.logger.LogError("Failed request with status code {ResponseStatusCode} (message = '{ResponseReasonPhrase}', error body = '{ErrorBody}').", nextResponse.StatusCode, nextResponse.ReasonPhrase, errorBody);
                errorMessage = nextResponse.ReasonPhrase;
                break;
            }
            
            if(nextResponse.StatusCode is HttpStatusCode.InternalServerError)
            {
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.CloudOff, string.Format(TB("We tried to communicate with the LLM provider '{0}' (type={1}). The server might be down or having issues. The provider message is: '{2}'"), this.InstanceName, this.Provider, nextResponse.ReasonPhrase)));
                this.logger.LogError("Failed request with status code {ResponseStatusCode} (message = '{ResponseReasonPhrase}', error body = '{ErrorBody}').", nextResponse.StatusCode, nextResponse.ReasonPhrase, errorBody);
                errorMessage = nextResponse.ReasonPhrase;
                break;
            }
            
            if(nextResponse.StatusCode is HttpStatusCode.ServiceUnavailable)
            {
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.CloudOff, string.Format(TB("We tried to communicate with the LLM provider '{0}' (type={1}). The provider is overloaded. The message is: '{2}'"), this.InstanceName, this.Provider, nextResponse.ReasonPhrase)));
                this.logger.LogError("Failed request with status code {ResponseStatusCode} (message = '{ResponseReasonPhrase}', error body = '{ErrorBody}').", nextResponse.StatusCode, nextResponse.ReasonPhrase, errorBody);
                errorMessage = nextResponse.ReasonPhrase;
                break;
            }

            errorMessage = nextResponse.ReasonPhrase;
            var timeSeconds = Math.Pow(RETRY_DELAY_SECONDS, retry + 1);
            if(timeSeconds > 90)
                timeSeconds = 90;
            
            this.logger.LogDebug("Failed request with status code {ResponseStatusCode} (message = '{ErrorMessage}'). Retrying in {TimeSeconds:0.00} seconds.", nextResponse.StatusCode, errorMessage, timeSeconds);
            await Task.Delay(TimeSpan.FromSeconds(timeSeconds), effectiveCancellationToken);
        }
        
        if(retry >= MAX_RETRIES || !string.IsNullOrWhiteSpace(errorMessage))
        {
            if (lastProviderRequestFailure is not ProviderRequestFailureReason.NONE)
            {
                var userMessage = this.GetProviderRequestFailureUserMessage(lastProviderRequestFailure);
                this.logger.LogError("The request to provider '{ProviderInstanceName}' (provider={ProviderType}) failed after {MaxRetries} retries with status code {ResponseStatusCode} (message = '{ResponseReasonPhrase}', error body = '{ErrorBody}'): {ErrorMessage}", this.InstanceName, this.Provider, MAX_RETRIES, lastResponseStatusCode, lastResponseReasonPhrase, lastErrorBody, userMessage);
                throw new ProviderRequestException(lastProviderRequestFailure, userMessage, lastResponseStatusCode, lastResponseReasonPhrase, lastErrorBody);
            }

            await MessageBus.INSTANCE.SendError(new DataErrorMessage(Icons.Material.Filled.CloudOff, string.Format(TB("We tried to communicate with the LLM provider '{0}' (type={1}). Even after {2} retries, there were some problems with the request. The provider message is: '{3}'."), this.InstanceName, this.Provider, MAX_RETRIES, errorMessage)));
            return new HttpRateLimitedStreamResult(false, true, errorMessage ?? $"Failed after {MAX_RETRIES} retries; no provider message available", response);
        }

        return new HttpRateLimitedStreamResult(true, false, string.Empty, response);
    }

    /// <summary>
    /// Streams the chat completion from the provider using the Chat Completion API.
    /// </summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <param name="requestBuilder">A function that builds the request.</param>
    /// <param name="token">The cancellation token to use.</param>
    /// <typeparam name="TDelta">The type of the delta lines inside the stream.</typeparam>
    /// <typeparam name="TAnnotation">The type of the annotation lines inside the stream.</typeparam>
    /// <returns>The stream of content chunks.</returns>
    protected async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletionInternal<TDelta, TAnnotation>(string providerName, Func<Task<HttpRequestMessage>> requestBuilder, [EnumeratorCancellation] CancellationToken token = default) where TDelta : IResponseStreamLine where TAnnotation : IAnnotationStreamLine
    {
        // Check if annotations are supported:
        var annotationSupported = typeof(TAnnotation) != typeof(NoResponsesAnnotationStreamLine) && typeof(TAnnotation) != typeof(NoChatCompletionAnnotationStreamLine);
        
        StreamReader? streamReader = null;
        using var timeoutTokenSource = ExternalHttpClientTimeout.CreateTimeoutTokenSource(token);
        var timeoutToken = timeoutTokenSource.Token;
        try
        {
            // Send the request using exponential backoff:
            var responseData = await this.SendRequest(requestBuilder, token, timeoutToken);
            if(responseData.IsFailedAfterAllRetries)
            {
                this.logger.LogError($"The {providerName} chat completion failed: {responseData.ErrorMessage}");
                yield break;
            }
            
            // Open the response stream:
            var providerStream = await responseData.Response!.Content.ReadAsStreamAsync(timeoutToken);

            // Add a stream reader to read the stream, line by line:
            streamReader = new StreamReader(providerStream);
        }
        catch(ProviderRequestException)
        {
            throw;
        }
        catch(Exception e)
        {
            if (token.IsCancellationRequested)
            {
                this.logger.LogWarning("The user canceled the chat completion request for {ProviderName} '{ProviderInstanceName}' before the response stream was opened.", providerName, this.InstanceName);
            }
            else if (this.IsTimeoutException(e, token))
            {
                await this.SendTimeoutError("opening the chat response stream");
                this.logger.LogError(e, "Timed out while opening the chat completion stream from {ProviderName} '{ProviderInstanceName}'.", providerName, this.InstanceName);
            }
            else
            {
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Stream, string.Format(TB("Tried to communicate with the LLM provider '{0}'. There were some problems with the request. The provider message is: '{1}'"), this.InstanceName, e.Message)));
                this.logger.LogError($"Failed to stream chat completion from {providerName} '{this.InstanceName}': {e.Message}");
            }
        }

        if (streamReader is null)
            yield break;
        
        //
        // Read the stream, line by line:
        //
        while (true)
        {
            try
            {
                if(streamReader.EndOfStream)
                    break;
            }
            catch (Exception e)
            {
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Stream, string.Format(TB("Tried to stream the LLM provider '{0}' answer. There were some problems with the stream. The message is: '{1}'"), this.InstanceName, e.Message)));
                this.logger.LogWarning($"Failed to read the end-of-stream state from {providerName} '{this.InstanceName}': {e.Message}");
                break;
            }

            // Check if the token is canceled:
            if (token.IsCancellationRequested)
            {
                this.logger.LogWarning($"The user canceled the chat completion for {providerName} '{this.InstanceName}'.");
                streamReader.Close();
                yield break;
            }

            //
            // Read the next line:
            //
            string? line;
            try
            {
                line = await streamReader.ReadLineAsync(timeoutToken);
            }
            catch (Exception e)
            {
                if (token.IsCancellationRequested)
                {
                    this.logger.LogWarning("The user canceled the chat completion stream for {ProviderName} '{ProviderInstanceName}' while reading the next chunk.", providerName, this.InstanceName);
                }
                else if (this.IsTimeoutException(e, token))
                {
                    await this.SendTimeoutError("reading the chat response stream");
                    this.logger.LogError(e, "Timed out while reading the chat stream from {ProviderName} '{ProviderInstanceName}'.", providerName, this.InstanceName);
                }
                else
                {
                    await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Stream, string.Format(TB("Tried to stream the LLM provider '{0}' answer. Was not able to read the stream. The message is: '{1}'"), this.InstanceName, e.Message)));
                    this.logger.LogError($"Failed to read the stream from {providerName} '{this.InstanceName}': {e.Message}");
                }

                break;
            }

            if (line is null)
                break;

            // Skip empty lines:
            if (string.IsNullOrWhiteSpace(line))
                continue;
            
            if (this.TryCreateProviderRequestExceptionFromStreamLine(providerName, line, out var providerRequestException))
                throw providerRequestException;

            // Skip lines that do not start with "data: ". Regard
            // to the specification, we only want to read the data lines:
            if (!line.StartsWith("data: ", StringComparison.InvariantCulture))
                continue;

            // Check if the line is the end of the stream:
            if (line.StartsWith("data: [DONE]", StringComparison.InvariantCulture))
                yield break;

            //
            // Process annotation lines:
            //
            if (annotationSupported && line.Contains("""
                                                     "annotations":[
                                                     """, StringComparison.InvariantCulture))
            {
                TAnnotation? providerResponse;
                
                try
                {
                    // We know that the line starts with "data: ". Hence, we can
                    // skip the first 6 characters to get the JSON data after that.
                    var jsonData = line[6..];

                    // Deserialize the JSON data:
                    providerResponse = JsonSerializer.Deserialize<TAnnotation>(jsonData, JSON_SERIALIZER_OPTIONS);

                    if (providerResponse is null)
                        continue;
                }
                catch
                {
                    // Skip invalid JSON data:
                    continue;
                }

                // Skip empty responses:
                if (!providerResponse.ContainsSources())
                    continue;

                // Yield the response:
                yield return new(string.Empty, providerResponse.GetSources());
            }
            
            //
            // Process delta lines:
            //
            else
            {
                TDelta? providerResponse;
                try
                {
                    // We know that the line starts with "data: ". Hence, we can
                    // skip the first 6 characters to get the JSON data after that.
                    var jsonData = line[6..];

                    // Deserialize the JSON data:
                    providerResponse = JsonSerializer.Deserialize<TDelta>(jsonData, JSON_SERIALIZER_OPTIONS);

                    if (providerResponse is null)
                        continue;
                }
                catch
                {
                    // Skip invalid JSON data:
                    continue;
                }

                // Skip empty responses:
                if (!providerResponse.ContainsContent())
                    continue;

                // Yield the response:
                yield return providerResponse.GetContent();
            }
        }
        
        streamReader.Dispose();
    }

    /// <summary>
    /// Streams the chat completion from the provider using the Responses API.
    /// </summary>
    /// <param name="providerName">The name of the provider.</param>
    /// <param name="requestBuilder">A function that builds the request.</param>
    /// <param name="token">The cancellation token to use.</param>
    /// <typeparam name="TDelta">The type of the delta lines inside the stream.</typeparam>
    /// <typeparam name="TAnnotation">The type of the annotation lines inside the stream.</typeparam>
    /// <returns>The stream of content chunks.</returns>
    protected async IAsyncEnumerable<ContentStreamChunk> StreamResponsesInternal<TDelta, TAnnotation>(string providerName, Func<Task<HttpRequestMessage>> requestBuilder, [EnumeratorCancellation] CancellationToken token = default) where TDelta : IResponseStreamLine where TAnnotation : IAnnotationStreamLine
    {
        // Check if annotations are supported:
        var annotationSupported = typeof(TAnnotation) != typeof(NoResponsesAnnotationStreamLine) && typeof(TAnnotation) != typeof(NoChatCompletionAnnotationStreamLine);
        
        StreamReader? streamReader = null;
        using var timeoutTokenSource = ExternalHttpClientTimeout.CreateTimeoutTokenSource(token);
        var timeoutToken = timeoutTokenSource.Token;
        try
        {
            // Send the request using exponential backoff:
            var responseData = await this.SendRequest(requestBuilder, token, timeoutToken);
            if(responseData.IsFailedAfterAllRetries)
            {
                this.logger.LogError($"The {providerName} responses call failed: {responseData.ErrorMessage}");
                yield break;
            }
            
            // Open the response stream:
            var providerStream = await responseData.Response!.Content.ReadAsStreamAsync(timeoutToken);

            // Add a stream reader to read the stream, line by line:
            streamReader = new StreamReader(providerStream);
        }
        catch(ProviderRequestException)
        {
            throw;
        }
        catch(Exception e)
        {
            if (token.IsCancellationRequested)
            {
                this.logger.LogWarning("The user canceled the responses request for {ProviderName} '{ProviderInstanceName}' before the response stream was opened.", providerName, this.InstanceName);
            }
            else if (this.IsTimeoutException(e, token))
            {
                await this.SendTimeoutError("opening the chat response stream");
                this.logger.LogError(e, "Timed out while opening the responses stream from {ProviderName} '{ProviderInstanceName}'.", providerName, this.InstanceName);
            }
            else
            {
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Stream, string.Format(TB("Tried to communicate with the LLM provider '{0}'. There were some problems with the request. The provider message is: '{1}'"), this.InstanceName, e.Message)));
                this.logger.LogError($"Failed to stream responses from {providerName} '{this.InstanceName}': {e.Message}");
            }
        }

        if (streamReader is null)
            yield break;
        
        //
        // Read the stream, line by line:
        //
        while (true)
        {
            try
            {
                if(streamReader.EndOfStream)
                    break;
            }
            catch (Exception e)
            {
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Stream, string.Format(TB("Tried to stream the LLM provider '{0}' answer. There were some problems with the stream. The message is: '{1}'"), this.InstanceName, e.Message)));
                this.logger.LogWarning($"Failed to read the end-of-stream state from {providerName} '{this.InstanceName}': {e.Message}");
                break;
            }

            // Check if the token is canceled:
            if (token.IsCancellationRequested)
            {
                this.logger.LogWarning($"The user canceled the responses for {providerName} '{this.InstanceName}'.");
                streamReader.Close();
                yield break;
            }

            //
            // Read the next line:
            //
            string? line;
            try
            {
                line = await streamReader.ReadLineAsync(timeoutToken);
            }
            catch (Exception e)
            {
                if (token.IsCancellationRequested)
                {
                    this.logger.LogWarning("The user canceled the responses stream for {ProviderName} '{ProviderInstanceName}' while reading the next chunk.", providerName, this.InstanceName);
                }
                else if (this.IsTimeoutException(e, token))
                {
                    await this.SendTimeoutError("reading the chat response stream");
                    this.logger.LogError(e, "Timed out while reading the responses stream from {ProviderName} '{ProviderInstanceName}'.", providerName, this.InstanceName);
                }
                else
                {
                    await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Stream, string.Format(TB("Tried to stream the LLM provider '{0}' answer. Was not able to read the stream. The message is: '{1}'"), this.InstanceName, e.Message)));
                    this.logger.LogError($"Failed to read the stream from {providerName} '{this.InstanceName}': {e.Message}");
                }

                break;
            }

            if (line is null)
                break;

            // Skip empty lines:
            if (string.IsNullOrWhiteSpace(line))
                continue;
            
            if (this.TryCreateProviderRequestExceptionFromStreamLine(providerName, line, out var providerRequestException))
                throw providerRequestException;

            // Check if the line is the end of the stream:
            if (line.StartsWith("event: response.completed", StringComparison.InvariantCulture))
                yield break;
            
            //
            // Find delta lines:
            //
            if (line.StartsWith("""
                                data: {"type":"response.output_text.delta"
                                """, StringComparison.InvariantCulture))
            {
                TDelta? providerResponse;
                try
                {
                    // We know that the line starts with "data: ". Hence, we can
                    // skip the first 6 characters to get the JSON data after that.
                    var jsonData = line[6..];

                    // Deserialize the JSON data:
                    providerResponse = JsonSerializer.Deserialize<TDelta>(jsonData, JSON_SERIALIZER_OPTIONS);

                    if (providerResponse is null)
                        continue;
                }
                catch
                {
                    // Skip invalid JSON data:
                    continue;
                }

                // Skip empty responses:
                if (!providerResponse.ContainsContent())
                    continue;

                // Yield the response:
                yield return providerResponse.GetContent();
            }
            
            //
            // Find annotation added lines:
            //
            else if (annotationSupported && line.StartsWith(
                         """
                         data: {"type":"response.output_text.annotation.added"
                         """, StringComparison.InvariantCulture))
            {
                TAnnotation? providerResponse;
                try
                {
                    // We know that the line starts with "data: ". Hence, we can
                    // skip the first 6 characters to get the JSON data after that.
                    var jsonData = line[6..];

                    // Deserialize the JSON data:
                    providerResponse = JsonSerializer.Deserialize<TAnnotation>(jsonData, JSON_SERIALIZER_OPTIONS);

                    if (providerResponse is null)
                        continue;
                }
                catch
                {
                    // Skip invalid JSON data:
                    continue;
                }

                // Skip empty responses:
                if (!providerResponse.ContainsSources())
                    continue;

                // Yield the response:
                yield return new(string.Empty, providerResponse.GetSources());
            }
        }
        
        streamReader.Dispose();
    }

    /// <summary>
    /// Streams the chat completion from an OpenAI-compatible provider using the Chat Completion API.
    /// </summary>
    /// <param name="providerName">The provider name for logging and error reporting.</param>
    /// <param name="chatModel">The selected chat model.</param>
    /// <param name="chatThread">The current chat thread.</param>
    /// <param name="settingsManager">The settings manager.</param>
    /// <param name="requestFactory">Builds the provider-specific request body.</param>
    /// <param name="storeType">The secret store type.</param>
    /// <param name="isTryingSecret">Whether the API key is optional.</param>
    /// <param name="systemPromptRole">The system prompt role to use.</param>
    /// <param name="requestPath">The request path, relative to the provider base URL.</param>
    /// <param name="headersAction">Optional additional headers to add.</param>
    /// <param name="token">The cancellation token.</param>
    /// <typeparam name="TRequest">The request DTO type.</typeparam>
    /// <typeparam name="TDelta">The delta stream line type.</typeparam>
    /// <typeparam name="TAnnotation">The annotation stream line type.</typeparam>
    /// <returns>The streamed content chunks.</returns>
    protected async IAsyncEnumerable<ContentStreamChunk> StreamOpenAICompatibleChatCompletion<TRequest, TDelta, TAnnotation>(
        string providerName,
        Model chatModel,
        ChatThread chatThread,
        SettingsManager settingsManager,
        Func<TextMessage, IDictionary<string, object>, IList<object>?, Task<TRequest>> requestFactory,
        SecretStoreType storeType = SecretStoreType.LLM_PROVIDER,
        bool isTryingSecret = false,
        string systemPromptRole = "system",
        string requestPath = "chat/completions",
        Action<HttpRequestHeaders>? headersAction = null,
        [EnumeratorCancellation] CancellationToken token = default)
        where TRequest : ChatCompletionAPIRequest
        where TDelta : IResponseStreamLine
        where TAnnotation : IAnnotationStreamLine
    {
        // Get the API key:
        var requestedSecret = await RUST_SERVICE.GetAPIKey(this, storeType, isTrying: isTryingSecret);
        if(!requestedSecret.Success && !isTryingSecret)
            yield break;

        // Parse the API parameters:
        var apiParameters = this.ParseAdditionalApiParameters();

        var toolRegistry = Program.SERVICE_PROVIDER.GetService<ToolRegistry>();
        var toolExecutor = Program.SERVICE_PROVIDER.GetService<ToolExecutor>();
        var currentAssistantContent = chatThread.Blocks.LastOrDefault(x => x.Role is ChatRole.AI)?.Content as ContentText;
        currentAssistantContent?.ToolInvocations.Clear();

        async Task ResetToolRuntimeStatusAsync()
        {
            if (currentAssistantContent is null)
                return;

            currentAssistantContent.ToolRuntimeStatus = new();
            await currentAssistantContent.StreamingEvent();
        }

        async Task ShowToolRuntimeStatusAsync(IEnumerable<string> toolNames)
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

        TextMessage systemPrompt;
        if (toolRegistry is not null && toolExecutor is not null)
        {
            var runnableTools = await toolRegistry.GetRunnableToolsAsync(
                this.CreateSettingsProvider(chatModel),
                chatThread.RuntimeComponent,
                chatThread.RuntimeSelectedToolIds,
                this.Provider.GetModelCapabilities(chatModel),
                this.Provider.GetConfidence(settingsManager).Level,
                settingsManager.IsToolSelectionVisible(chatThread.RuntimeComponent));

            systemPrompt = new TextMessage
            {
                Role = systemPromptRole,
                Content = chatThread.PrepareSystemPrompt(settingsManager, runnableTools.Select(x => x.Definition)),
            };

            if (runnableTools.Count > 0)
            {
                var providerTools = runnableTools.Select(x => ProviderToolAdapters.ToChatCompletionTool(x.Definition)).ToList();

                var internalMessages = new List<IMessageBase>();
                var toolCallCount = 0;
                while (true)
                {
                    ChatCompletionAPIRequest requestDtoBase = await requestFactory(systemPrompt, apiParameters, providerTools);
                    var requestDto = requestDtoBase with
                    {
                        Messages = [..requestDtoBase.Messages, ..internalMessages],
                        Stream = false,
                    };
                    var response = await this.ExecuteChatCompletionRequest(requestDto, requestPath, requestedSecret, headersAction, token);
                    var responseMessage = response?.Choices.FirstOrDefault()?.Message;
                    if (responseMessage is null)
                    {
                        await ResetToolRuntimeStatusAsync();
                        yield break;
                    }

                    if (responseMessage.ToolCalls.Count == 0)
                    {
                        await ResetToolRuntimeStatusAsync();
                        if (!string.IsNullOrWhiteSpace(responseMessage.Content))
                            yield return new ContentStreamChunk(responseMessage.Content, []);
                        else if (toolCallCount > 0)
                            yield return new ContentStreamChunk("The model completed the tool call but did not return a final answer.", []);

                        yield break;
                    }

                    await ShowToolRuntimeStatusAsync(responseMessage.ToolCalls
                        .Select(x => runnableTools.FirstOrDefault(tool => tool.Definition.Function.Name.Equals(x.Function.Name, StringComparison.Ordinal)).Implementation?.GetDisplayName() ?? x.Function.Name));

                    internalMessages.Add(new AssistantToolCallMessage
                    {
                        Content = responseMessage.Content,
                        ToolCalls = responseMessage.ToolCalls,
                    });
                    
                    foreach (var toolCall in responseMessage.ToolCalls)
                    {
                        toolCallCount++;
                        if (toolCallCount > ToolSelectionRules.MAX_TOOL_CALLS)
                        {
                            var limitMessage = ToolSelectionRules.GetMaxToolCallsLimitMessage();
                            currentAssistantContent?.ToolInvocations.Add(new ToolInvocationTrace
                            {
                                Order = toolCallCount,
                                ToolId = toolCall.Function.Name,
                                ToolName = toolCall.Function.Name,
                                ToolCallId = toolCall.Id,
                                Status = ToolInvocationTraceStatus.BLOCKED,
                                StatusMessage = limitMessage,
                                Result = limitMessage,
                            });
                            await ResetToolRuntimeStatusAsync();
                            yield return new ContentStreamChunk(limitMessage, []);
                            yield break;
                        }

                        var (toolContent, trace) = await toolExecutor.ExecuteAsync(
                            toolCall.Id,
                            toolCall.Function.Name,
                            toolCall.Function.Arguments,
                            runnableTools,
                            this.Provider.GetConfidence(settingsManager).Level,
                            toolCallCount,
                            token);

                        currentAssistantContent?.ToolInvocations.Add(trace);
                        internalMessages.Add(new ToolResultMessage
                        {
                            Content = toolContent,
                            ToolCallId = toolCall.Id,
                            Name = toolCall.Function.Name,
                        });
                    }

                    if (currentAssistantContent is not null)
                        await currentAssistantContent.StreamingEvent();
                }
            }

        }
        else
        {
            systemPrompt = new TextMessage
            {
                Role = systemPromptRole,
                Content = chatThread.PrepareSystemPrompt(settingsManager),
            };
        }

        // Prepare the provider HTTP chat request:
        var providerChatRequest = JsonSerializer.Serialize(await requestFactory(systemPrompt, apiParameters, null), JSON_SERIALIZER_OPTIONS);

        async Task<HttpRequestMessage> RequestBuilder()
        {
            // Build the HTTP post request:
            var request = new HttpRequestMessage(HttpMethod.Post, requestPath);

            // Set the authorization header:
            if (requestedSecret.Success)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await requestedSecret.Secret.Decrypt(ENCRYPTION));

            // Set provider-specific headers:
            headersAction?.Invoke(request.Headers);

            // Set the content:
            request.Content = new StringContent(providerChatRequest, Encoding.UTF8, "application/json");
            return request;
        }

        await foreach (var content in this.StreamChatCompletionInternal<TDelta, TAnnotation>(providerName, RequestBuilder, token))
            yield return content;
    }

    private AIStudio.Settings.Provider CreateSettingsProvider(Model chatModel) => new()
    {
        UsedLLMProvider = this.Provider,
        Model = chatModel,
        InstanceName = this.InstanceName,
    };

    private async Task<ChatCompletionResponse?> ExecuteChatCompletionRequest(
        ChatCompletionAPIRequest requestDto,
        string requestPath,
        RequestedSecret requestedSecret,
        Action<HttpRequestHeaders>? headersAction,
        CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, requestPath);
        if (requestedSecret.Success)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await requestedSecret.Secret.Decrypt(ENCRYPTION));

        headersAction?.Invoke(request.Headers);
        request.Content = new StringContent(JsonSerializer.Serialize(requestDto, JSON_SERIALIZER_OPTIONS), Encoding.UTF8, "application/json");

        using var response = await this.HttpClient.SendAsync(request, token);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(token);
            this.logger.LogError("Tool calling chat completion request failed with status code {ResponseStatusCode} and body: '{ResponseBody}'.", response.StatusCode, responseBody);
            await MessageBus.INSTANCE.SendError(new(
                Icons.Material.Filled.Build,
                string.Format(TB("The tool calling request failed with status code {0}. See the logs for details."), (int)response.StatusCode)));
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JSON_SERIALIZER_OPTIONS, token);
    }

    protected async Task<TranscriptionResult> PerformStandardTranscriptionRequest(RequestedSecret requestedSecret, Model transcriptionModel, string audioFilePath, Host host = Host.NONE, CancellationToken token = default)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            var mimeType = Builder.FromFilename(audioFilePath);
        
            await using var fileStream = File.OpenRead(audioFilePath);
            using var fileContent = new StreamContent(fileStream);
            
            // Set the content type based on the file extension:
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
            
            // Add the file content to the form data:
            form.Add(fileContent, "file", Path.GetFileName(audioFilePath));

            //
            // Add the model name to the form data. Ensure that a model name is always provided.
            // Otherwise, the StringContent constructor will throw an exception.
            //
            var modelName = transcriptionModel.Id;
            if (string.IsNullOrWhiteSpace(modelName))
                modelName = "placeholder";
            
            form.Add(new StringContent(modelName), "model");

            using var request = new HttpRequestMessage(HttpMethod.Post, host.TranscriptionURL());
            request.Content = form;

            // Handle the authorization header based on the provider:
            switch (this.Provider)
            {
                case LLMProviders.SELF_HOSTED:
                    if(requestedSecret.Success)
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await requestedSecret.Secret.Decrypt(ENCRYPTION));
                    
                    break;
                
                case LLMProviders.FIREWORKS:
                    if(!requestedSecret.Success)
                    {
                        this.logger.LogError("No valid API key available for transcription request.");
                        return TranscriptionResult.Failure();
                    }
                    
                    request.Headers.Add("Authorization", await requestedSecret.Secret.Decrypt(ENCRYPTION));
                    break;
                
                default:
                    if(!requestedSecret.Success)
                    {
                        this.logger.LogError("No valid API key available for transcription request.");
                        return TranscriptionResult.Failure();
                    }
                    
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await requestedSecret.Secret.Decrypt(ENCRYPTION));
                    break;
            }
            
            using var response = await this.HttpClient.SendAsync(request, token);
            var responseBody = await response.Content.ReadAsStringAsync(token);
        
            if (!response.IsSuccessStatusCode)
            {
                this.logger.LogError("Transcription request failed with status code {ResponseStatusCode} and body: '{ResponseBody}'.", response.StatusCode, responseBody);
                var providerRequestFailure = this.ClassifyProviderRequestFailure(response.StatusCode, responseBody);
                return TranscriptionResult.Failure(this.GetProviderRequestFailureUserMessage(providerRequestFailure));
            }

            var transcriptionResponse = JsonSerializer.Deserialize<TranscriptionResponse>(responseBody, JSON_SERIALIZER_OPTIONS);
            if(transcriptionResponse is null)
            {
                this.logger.LogError("Was not able to deserialize the transcription response.");
                return TranscriptionResult.Failure();
            }

            return TranscriptionResult.FromText(transcriptionResponse.Text);
        }
        catch (Exception e)
        {
            if (this.IsTimeoutException(e, token))
                await this.SendTimeoutError("transcribing audio");

            this.logger.LogError("Failed to perform transcription request: '{Message}'.", e.Message);
            return TranscriptionResult.Failure();
        }
    }
    
    protected async Task<IReadOnlyList<IReadOnlyList<float>>> PerformStandardTextEmbeddingRequest(RequestedSecret requestedSecret, Model embeddingModel, Host host = Host.NONE, CancellationToken token = default, params List<string> texts)
    {
        try
        {
            //
            // Add the model name to the form data. Ensure that a model name is always provided.
            // Otherwise, the StringContent constructor will throw an exception.
            //
            var modelName = embeddingModel.Id;
            if (string.IsNullOrWhiteSpace(modelName))
                modelName = "placeholder";
            
            // Prepare the HTTP embedding request:
            var payload = new
            {
                model = modelName,
                input = texts,
                encoding_format = "float"
            };
            
            var embeddingRequest = JsonSerializer.Serialize(payload, JSON_SERIALIZER_OPTIONS);
            using var request = new HttpRequestMessage(HttpMethod.Post, host.EmbeddingURL());

            // Handle the authorization header based on the provider:
            switch (this.Provider)
            {
                case LLMProviders.SELF_HOSTED:
                    if(requestedSecret.Success)
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await requestedSecret.Secret.Decrypt(ENCRYPTION));
                    
                    break;
                
                default:
                    if(!requestedSecret.Success)
                    {
                        this.logger.LogError("No valid API key available for embedding request.");
                        return [];
                    }
                    
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await requestedSecret.Secret.Decrypt(ENCRYPTION));
                    break;
            }
            
            // Set the content:
            request.Content = new StringContent(embeddingRequest, Encoding.UTF8, "application/json");
            using var response = await this.HttpClient.SendAsync(request, token);
            var responseBody = await response.Content.ReadAsStringAsync(token);
        
            if (!response.IsSuccessStatusCode)
            {
                this.logger.LogError("Embedding request failed with status code {ResponseStatusCode} and body: '{ResponseBody}'.", response.StatusCode, responseBody);
                var providerRequestFailure = this.ClassifyProviderRequestFailure(response.StatusCode, responseBody);
                var userMessage = this.GetProviderRequestFailureUserMessage(providerRequestFailure);
                if (!string.IsNullOrWhiteSpace(userMessage))
                    await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.CloudOff, userMessage));

                return [];
            }

            var embeddingResponse = JsonSerializer.Deserialize<EmbeddingResponse>(responseBody, JSON_SERIALIZER_OPTIONS);
            if (embeddingResponse is { Data: not null })
            {
                return embeddingResponse.Data
                    .Select(d => d.Embedding?.ToArray() ?? [])
                    .Cast<IReadOnlyList<float>>()
                    .ToArray();
            }
            else
            {
                this.logger.LogError("Was not able to deserialize the embedding response.");
                return [];
            }
        }
        catch (Exception e)
        {
            if (this.IsTimeoutException(e, token))
                await this.SendTimeoutError("creating embeddings");

            this.logger.LogError("Failed to perform embedding request: '{Message}'.", e.Message);
            return [];
        }
    }
    
    /// <summary>
    /// Parse and convert API parameters from a provided JSON string into a dictionary,
    /// optionally merging additional parameters and removing specific keys.
    /// </summary>
    /// <param name="keysToRemove">Optional list of keys to remove from the final dictionary
    /// (case-insensitive). The parameters stream, model, and messages are removed by default.</param>
    protected IDictionary<string, object> ParseAdditionalApiParameters(
        params string[] keysToRemove)
    {
        if(string.IsNullOrWhiteSpace(this.AdditionalJsonApiParameters))
            return new Dictionary<string, object>();
        
        try
        {
            // Wrap the user-provided parameters in curly brackets to form a valid JSON object:
            var json = $"{{{this.AdditionalJsonApiParameters}}}";
            var jsonDoc = JsonSerializer.Deserialize<JsonElement>(json, JSON_SERIALIZER_OPTIONS);
            var dict = ConvertToDictionary(jsonDoc);

            // Some keys are always removed because we set them:
            var removeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (keysToRemove.Length > 0)
                removeSet.UnionWith(keysToRemove);
            
            removeSet.Add("stream");
            removeSet.Add("model");
            removeSet.Add("messages");

            // Remove the specified keys (case-insensitive):
            if (removeSet.Count > 0)
            {
                foreach (var key in dict.Keys.ToList())
                {
                    if (removeSet.Contains(key))
                        dict.Remove(key);
                }
            }

            return dict;
        }
        catch (JsonException ex)
        {
            this.logger.LogError("Failed to parse additional API parameters: {ExceptionMessage}", ex.Message);
            return new Dictionary<string, object>();
        }
    }
    
    protected static bool TryPopIntParameter(IDictionary<string, object> parameters, string key, out int value)
    {
        value = default;
        if (!TryPopParameter(parameters, key, out var raw) || raw is null)
            return false;
        
        switch (raw)
        {
            case int i:
                value = i;
                return true;
            
            case long l when l is >= int.MinValue and <= int.MaxValue:
                value = (int)l;
                return true;
            
            case double d when d is >= int.MinValue and <= int.MaxValue:
                value = (int)d;
                return true;
            
            case decimal m when m is >= int.MinValue and <= int.MaxValue:
                value = (int)m;
                return true;
        }
        
        return false;
    }
    
    protected static bool TryPopBoolParameter(IDictionary<string, object> parameters, string key, out bool value)
    {
        value = default;
        if (!TryPopParameter(parameters, key, out var raw) || raw is null)
            return false;
        
        switch (raw)
        {
            case bool b:
                value = b;
                return true;
            
            case string s when bool.TryParse(s, out var parsed):
                value = parsed;
                return true;
            
            case int i:
                value = i != 0;
                return true;
            
            case long l:
                value = l != 0;
                return true;
            
            case double d:
                value = Math.Abs(d) > double.Epsilon;
                return true;
            
            case decimal m:
                value = m != 0;
                return true;
        }
        
        return false;
    }
    
    private static bool TryPopParameter(IDictionary<string, object> parameters, string key, out object? value)
    {
        value = null;
        if (parameters.Count == 0)
            return false;
        
        var foundKey = parameters.Keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
        if (foundKey is null)
            return false;
        
        value = parameters[foundKey];
        parameters.Remove(foundKey);
        return true;
    }

    private static IDictionary<string, object> ConvertToDictionary(JsonElement element)
    {
        return element.EnumerateObject()
            .ToDictionary<JsonProperty, string, object>(
                p => p.Name,
                p => ConvertJsonValue(p.Value) ?? string.Empty
            );
    }

    private static object? ConvertJsonValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt32(out var i) ? i :
            element.TryGetInt64(out var l) ? l :
            element.TryGetDouble(out var d) ? d :
            element.GetDecimal(),
        JsonValueKind.True or JsonValueKind.False => element.GetBoolean(),
        JsonValueKind.Null => string.Empty,
        JsonValueKind.Object => ConvertToDictionary(element),
        JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonValue).ToList(),
        
        _ => string.Empty,
    };
}
