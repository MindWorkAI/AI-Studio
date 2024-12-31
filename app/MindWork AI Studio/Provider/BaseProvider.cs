using AIStudio.Chat;

using RustService = AIStudio.Tools.RustService;

namespace AIStudio.Provider;

/// <summary>
/// The base class for all providers.
/// </summary>
public abstract class BaseProvider : IProvider, ISecretId
{
    /// <summary>
    /// The HTTP client to use it for all requests.
    /// </summary>
    protected readonly HttpClient httpClient = new();
    
    /// <summary>
    /// The logger to use.
    /// </summary>
    protected readonly ILogger logger;

    static BaseProvider()
    {
        RUST_SERVICE = Program.RUST_SERVICE;
        ENCRYPTION = Program.ENCRYPTION;
    }

    protected static readonly RustService RUST_SERVICE;
    
    protected static readonly Encryption ENCRYPTION;

    /// <summary>
    /// Constructor for the base provider.
    /// </summary>
    /// <param name="url">The base URL for the provider.</param>
    /// <param name="loggerService">The logger service to use.</param>
    protected BaseProvider(string url, ILogger loggerService)
    {
        this.logger = loggerService;

        // Set the base URL:
        this.httpClient.BaseAddress = new(url);
    }
    
    #region Handling of IProvider, which all providers must implement
    
    /// <inheritdoc />
    public abstract string Id { get; }
    
    /// <inheritdoc />
    public abstract string InstanceName { get; set; }
    
    /// <inheritdoc />
    public abstract IAsyncEnumerable<string> StreamChatCompletion(Model chatModel, ChatThread chatThread, CancellationToken token = default);
    
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
    protected async Task<HttpRateLimitedStreamResult> SendRequest(Func<Task<HttpRequestMessage>> requestBuilder, CancellationToken token = default)
    {
        const int MAX_RETRIES = 6;
        const double RETRY_DELAY_SECONDS = 4;
        
        var retry = 0;
        var response = default(HttpResponseMessage);
        var errorMessage = string.Empty;
        while (retry++ < MAX_RETRIES)
        {
            using var request = await requestBuilder();
            
            // Send the request with the ResponseHeadersRead option.
            // This allows us to read the stream as soon as the headers are received.
            // This is important because we want to stream the responses.
            var nextResponse = await this.httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (nextResponse.IsSuccessStatusCode)
            {
                response = nextResponse;
                break;
            }

            errorMessage = nextResponse.ReasonPhrase;
            var timeSeconds = Math.Pow(RETRY_DELAY_SECONDS, retry + 1);
            if(timeSeconds > 90)
                timeSeconds = 90;
            
            this.logger.LogDebug($"Failed request with status code {nextResponse.StatusCode} (message = '{errorMessage}'). Retrying in {timeSeconds:0.00} seconds.");
            await Task.Delay(TimeSpan.FromSeconds(timeSeconds), token);
        }
        
        if(retry >= MAX_RETRIES)
            return new HttpRateLimitedStreamResult(false, true, errorMessage ?? $"Failed after {MAX_RETRIES} retries; no provider message available", response);
        
        return new HttpRateLimitedStreamResult(true, false, string.Empty, response);
    }
}