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
    /// Constructor for the base provider.
    /// </summary>
    /// <param name="url">The base URL for the provider.</param>
    protected BaseProvider(string url)
    {
        // Set the base URL:
        this.httpClient.BaseAddress = new(url);
    }
}