using RustService = AIStudio.Tools.RustService;

namespace AIStudio.Provider;

/// <summary>
/// The base class for all providers.
/// </summary>
public abstract class BaseProvider
{
    /// <summary>
    /// The HTTP client to use for all requests.
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
}