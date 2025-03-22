using AIStudio.Tools.ERIClient.DataModel;
using AIStudio.Tools.Services;

namespace AIStudio.Tools.ERIClient;

public interface IERIClient : IDisposable
{
    /// <summary>
    /// Retrieves the available authentication methods from the ERI server.
    /// </summary>
    /// <remarks>
    /// No authentication is required to retrieve the available authentication methods.
    /// </remarks>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The available authentication methods.</returns>
    public Task<APIResponse<List<AuthScheme>>> GetAuthMethodsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticate the user to the ERI server.
    /// </summary>
    /// <param name="rustService">The Rust service.</param>
    /// <param name="temporarySecret">The temporary secret when adding a new data source, and the secret is not yet stored in the OS.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authentication response.</returns>
    public Task<APIResponse<AuthResponse>> AuthenticateAsync(RustService rustService, string? temporarySecret = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves the data source information from the ERI server.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The data source information.</returns>
    public Task<APIResponse<DataSourceInfo>> GetDataSourceInfoAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves the embedding information from the ERI server.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of embedding information.</returns>
    public Task<APIResponse<List<EmbeddingInfo>>> GetEmbeddingInfoAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves the retrieval information from the ERI server.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of retrieval information.</returns>
    public Task<APIResponse<List<RetrievalInfo>>> GetRetrievalInfoAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executes a retrieval request on the ERI server.
    /// </summary>
    /// <param name="request">The retrieval request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The retrieved contexts to use for augmentation and generation.</returns>
    public Task<APIResponse<List<Context>>> ExecuteRetrievalAsync(RetrievalRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves the security requirements from the ERI server.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The security requirements.</returns>
    public Task<APIResponse<SecurityRequirements>> GetSecurityRequirementsAsync(CancellationToken cancellationToken = default);
}