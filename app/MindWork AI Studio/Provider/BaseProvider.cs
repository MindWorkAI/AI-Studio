using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider.OpenAI;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

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
    protected readonly HttpClient httpClient = new();
    
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
        Converters = { new AnnotationConverter() },
        AllowTrailingCommas = false
    };

    /// <summary>
    /// Constructor for the base provider.
    /// </summary>
    /// <param name="url">The base URL for the provider.</param>
    /// <param name="logger">The logger to use.</param>
    protected BaseProvider(string url, ILogger logger)
    {
        this.logger = logger;

        // Set the base URL:
        this.httpClient.BaseAddress = new(url);
    }
    
    #region Handling of IProvider, which all providers must implement
    
    /// <inheritdoc />
    public abstract string Id { get; }
    
    /// <inheritdoc />
    public abstract string InstanceName { get; set; }

    /// <inheritdoc />
    public string AdditionalJsonApiParameters { get; init; } = string.Empty;

    /// <inheritdoc />
    public abstract IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, CancellationToken token = default);
    
    /// <inheritdoc />
    public abstract IAsyncEnumerable<ImageURL> StreamImageCompletion(Model imageModel, string promptPositive, string promptNegative = FilterOperator.String.Empty, ImageURL referenceImageURL = default, CancellationToken token = default);
    
    /// <inheritdoc />
    public abstract Task<IEnumerable<Model>> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default);
    
    /// <inheritdoc />
    public abstract Task<IEnumerable<Model>> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default);
    
    /// <inheritdoc />
    public abstract Task<IEnumerable<Model>> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default);
    
    #endregion
    
    #region Implementation of ISecretId

    public string SecretId => this.Id;

    public string SecretName => this.InstanceName;

    #endregion
    
    /// <summary>
    /// Sends a request and handles rate limiting by exponential backoff.
    /// </summary>
    /// <param name="requestBuilder">A function that builds the request.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The status object of the request.</returns>
    private async Task<HttpRateLimitedStreamResult> SendRequest(Func<Task<HttpRequestMessage>> requestBuilder, CancellationToken token = default)
    {
        const int MAX_RETRIES = 6;
        const double RETRY_DELAY_SECONDS = 4;
        
        var retry = 0;
        var response = default(HttpResponseMessage);
        var errorMessage = string.Empty;
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
            var nextResponse = await this.httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (nextResponse.IsSuccessStatusCode)
            {
                response = nextResponse;
                break;
            }

            var errorBody = await nextResponse.Content.ReadAsStringAsync(token);
            if (nextResponse.StatusCode is HttpStatusCode.Forbidden)
            {
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Block, string.Format(TB("Tried to communicate with the LLM provider '{0}'. You might not be able to use this provider from your location. The provider message is: '{1}'"), this.InstanceName, nextResponse.ReasonPhrase)));
                this.logger.LogError("Failed request with status code {ResposeStatusCode} (message = '{ResponseReasonPhrase}', error body = '{ErrorBody}').", nextResponse.StatusCode, nextResponse.ReasonPhrase, errorBody);
                errorMessage = nextResponse.ReasonPhrase;
                break;
            }
            
            if(nextResponse.StatusCode is HttpStatusCode.BadRequest)
            {
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.CloudOff, string.Format(TB("Tried to communicate with the LLM provider '{0}'. The required message format might be changed. The provider message is: '{1}'"), this.InstanceName, nextResponse.ReasonPhrase)));
                this.logger.LogError("Failed request with status code {ResposeStatusCode} (message = '{ResponseReasonPhrase}', error body = '{ErrorBody}').", nextResponse.StatusCode, nextResponse.ReasonPhrase, errorBody);
                errorMessage = nextResponse.ReasonPhrase;
                break;
            }
            
            if(nextResponse.StatusCode is HttpStatusCode.NotFound)
            {
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.CloudOff, string.Format(TB("Tried to communicate with the LLM provider '{0}'. Something was not found. The provider message is: '{1}'"), this.InstanceName, nextResponse.ReasonPhrase)));
                this.logger.LogError("Failed request with status code {ResposeStatusCode} (message = '{ResponseReasonPhrase}', error body = '{ErrorBody}').", nextResponse.StatusCode, nextResponse.ReasonPhrase, errorBody);
                errorMessage = nextResponse.ReasonPhrase;
                break;
            }
            
            if(nextResponse.StatusCode is HttpStatusCode.Unauthorized)
            {
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Key, string.Format(TB("Tried to communicate with the LLM provider '{0}'. The API key might be invalid. The provider message is: '{1}'"), this.InstanceName, nextResponse.ReasonPhrase)));
                this.logger.LogError("Failed request with status code {ResposeStatusCode} (message = '{ResponseReasonPhrase}', error body = '{ErrorBody}').", nextResponse.StatusCode, nextResponse.ReasonPhrase, errorBody);
                errorMessage = nextResponse.ReasonPhrase;
                break;
            }
            
            if(nextResponse.StatusCode is HttpStatusCode.InternalServerError)
            {
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.CloudOff, string.Format(TB("Tried to communicate with the LLM provider '{0}'. The server might be down or having issues. The provider message is: '{1}'"), this.InstanceName, nextResponse.ReasonPhrase)));
                this.logger.LogError("Failed request with status code {ResposeStatusCode} (message = '{ResponseReasonPhrase}', error body = '{ErrorBody}').", nextResponse.StatusCode, nextResponse.ReasonPhrase, errorBody);
                errorMessage = nextResponse.ReasonPhrase;
                break;
            }
            
            if(nextResponse.StatusCode is HttpStatusCode.ServiceUnavailable)
            {
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.CloudOff, string.Format(TB("Tried to communicate with the LLM provider '{0}'. The provider is overloaded. The message is: '{1}'"), this.InstanceName, nextResponse.ReasonPhrase)));
                this.logger.LogError("Failed request with status code {ResposeStatusCode} (message = '{ResponseReasonPhrase}', error body = '{ErrorBody}').", nextResponse.StatusCode, nextResponse.ReasonPhrase, errorBody);
                errorMessage = nextResponse.ReasonPhrase;
                break;
            }

            errorMessage = nextResponse.ReasonPhrase;
            var timeSeconds = Math.Pow(RETRY_DELAY_SECONDS, retry + 1);
            if(timeSeconds > 90)
                timeSeconds = 90;
            
            this.logger.LogDebug("Failed request with status code {ResponseStatusCode} (message = '{ErrorMessage}'). Retrying in {TimeSeconds:0.00} seconds.", nextResponse.StatusCode, errorMessage, timeSeconds);
            await Task.Delay(TimeSpan.FromSeconds(timeSeconds), token);
        }
        
        if(retry >= MAX_RETRIES || !string.IsNullOrWhiteSpace(errorMessage))
        {
            await MessageBus.INSTANCE.SendError(new DataErrorMessage(Icons.Material.Filled.CloudOff, string.Format(TB("Tried to communicate with the LLM provider '{0}'. Even after {1} retries, there were some problems with the request. The provider message is: '{2}'."), this.InstanceName, MAX_RETRIES, errorMessage)));
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
        try
        {
            // Send the request using exponential backoff:
            var responseData = await this.SendRequest(requestBuilder, token);
            if(responseData.IsFailedAfterAllRetries)
            {
                this.logger.LogError($"The {providerName} chat completion failed: {responseData.ErrorMessage}");
                yield break;
            }
            
            // Open the response stream:
            var providerStream = await responseData.Response!.Content.ReadAsStreamAsync(token);

            // Add a stream reader to read the stream, line by line:
            streamReader = new StreamReader(providerStream);
        }
        catch(Exception e)
        {
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Stream, string.Format(TB("Tried to communicate with the LLM provider '{0}'. There were some problems with the request. The provider message is: '{1}'"), this.InstanceName, e.Message)));
            this.logger.LogError($"Failed to stream chat completion from {providerName} '{this.InstanceName}': {e.Message}");
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
                line = await streamReader.ReadLineAsync(token);
            }
            catch (Exception e)
            {
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Stream, string.Format(TB("Tried to stream the LLM provider '{0}' answer. Was not able to read the stream. The message is: '{1}'"), this.InstanceName, e.Message)));
                this.logger.LogError($"Failed to read the stream from {providerName} '{this.InstanceName}': {e.Message}");
                break;
            }

            // Skip empty lines:
            if (string.IsNullOrWhiteSpace(line))
                continue;
            
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
        try
        {
            // Send the request using exponential backoff:
            var responseData = await this.SendRequest(requestBuilder, token);
            if(responseData.IsFailedAfterAllRetries)
            {
                this.logger.LogError($"The {providerName} responses call failed: {responseData.ErrorMessage}");
                yield break;
            }
            
            // Open the response stream:
            var providerStream = await responseData.Response!.Content.ReadAsStreamAsync(token);

            // Add a stream reader to read the stream, line by line:
            streamReader = new StreamReader(providerStream);
        }
        catch(Exception e)
        {
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Stream, string.Format(TB("Tried to communicate with the LLM provider '{0}'. There were some problems with the request. The provider message is: '{1}'"), this.InstanceName, e.Message)));
            this.logger.LogError($"Failed to stream responses from {providerName} '{this.InstanceName}': {e.Message}");
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
                line = await streamReader.ReadLineAsync(token);
            }
            catch (Exception e)
            {
                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Stream, string.Format(TB("Tried to stream the LLM provider '{0}' answer. Was not able to read the stream. The message is: '{1}'"), this.InstanceName, e.Message)));
                this.logger.LogError($"Failed to read the stream from {providerName} '{this.InstanceName}': {e.Message}");
                break;
            }

            // Skip empty lines:
            if (string.IsNullOrWhiteSpace(line))
                continue;
            
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
    /// Parse and convert API parameters from a provided JSON string into a dictionary,
    /// optionally merging additional parameters and removing specific keys.
    /// </summary>
    /// <param name="keysToRemove">Optional list of keys to remove from the final dictionary
    /// (case-insensitive). The parameters stream, model, and messages are removed by default.</param>
    protected IDictionary<string, object> ParseAdditionalApiParameters(
        params List<string> keysToRemove)
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
            keysToRemove.Add("stream");
            keysToRemove.Add("model");
            keysToRemove.Add("messages");

            // Remove the specified keys (case-insensitive):
            var removeSet = new HashSet<string>(keysToRemove, StringComparer.OrdinalIgnoreCase);
            foreach (var key in removeSet)
                dict.Remove(key);

            return dict;
        }
        catch (JsonException ex)
        {
            this.logger.LogError("Failed to parse additional API parameters: {ExceptionMessage}", ex.Message);
            return new Dictionary<string, object>();
        }
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