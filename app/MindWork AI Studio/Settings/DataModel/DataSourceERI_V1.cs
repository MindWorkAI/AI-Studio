// ReSharper disable InconsistentNaming

using AIStudio.Assistants.ERI;
using AIStudio.Chat;
using AIStudio.Tools.ERIClient;
using AIStudio.Tools.ERIClient.DataModel;
using AIStudio.Tools.RAG;
using AIStudio.Tools.Services;

using ChatThread = AIStudio.Chat.ChatThread;
using ContentType = AIStudio.Tools.ERIClient.DataModel.ContentType;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// An external data source, accessed via an ERI server, cf. https://github.com/MindWorkAI/ERI.
/// </summary>
public readonly record struct DataSourceERI_V1 : IERIDataSource
{
    public DataSourceERI_V1()
    {
    }
    
    /// <inheritdoc />
    public uint Num { get; init; }

    /// <inheritdoc />
    public string Id { get; init; } = Guid.Empty.ToString();
    
    /// <inheritdoc />
    public string Name { get; init; } = string.Empty;
    
    /// <inheritdoc />
    public DataSourceType Type { get; init; } = DataSourceType.NONE;
    
    /// <inheritdoc />
    public string Hostname { get; init; } = string.Empty;
    
    /// <inheritdoc />
    public int Port { get; init; }

    /// <inheritdoc />
    public AuthMethod AuthMethod { get; init; } = AuthMethod.NONE;

    /// <inheritdoc />
    public string Username { get; init; } = string.Empty;

    /// <inheritdoc />
    public DataSourceSecurity SecurityPolicy { get; init; } = DataSourceSecurity.NOT_SPECIFIED;
    
    /// <inheritdoc />
    public ERIVersion Version { get; init; } = ERIVersion.V1;
    
    /// <inheritdoc />
    public string SelectedRetrievalId { get; init; } = string.Empty;

    /// <inheritdoc />
    public ushort MaxMatches { get; init; } = 10;
    
    /// <inheritdoc />
    public async Task<IReadOnlyList<IRetrievalContext>> RetrieveDataAsync(IContent lastUserPrompt, ChatThread thread, CancellationToken token = default)
    {
        // Important: Do not dispose the RustService here, as it is a singleton.
        var rustService = Program.SERVICE_PROVIDER.GetRequiredService<RustService>();
        var logger = Program.SERVICE_PROVIDER.GetRequiredService<ILogger<DataSourceERI_V1>>();
        
        using var eriClient = ERIClientFactory.Get(this.Version, this)!;
        var authResponse = await eriClient.AuthenticateAsync(rustService, cancellationToken: token);
        if (authResponse.Successful)
        {
            var retrievalRequest = new RetrievalRequest
            {
                LatestUserPromptType = lastUserPrompt.ToERIContentType,
                LatestUserPrompt = lastUserPrompt switch
                {
                    ContentText text => text.Text,
                    ContentImage image => await image.TryAsBase64(token) is (success: true, { } base64Image)
                        ? base64Image 
                        : string.Empty,
                    _ => string.Empty
                },
                
                Thread = await thread.ToERIChatThread(token),
                MaxMatches = this.MaxMatches,
                RetrievalProcessId = string.IsNullOrWhiteSpace(this.SelectedRetrievalId) ? null : this.SelectedRetrievalId,
                Parameters = null, // The ERI server selects useful default parameters
            };
            
            var retrievalResponse = await eriClient.ExecuteRetrievalAsync(retrievalRequest, token);
            if(retrievalResponse is { Successful: true, Data: not null })
            {
                //
                // Next, we have to transform the ERI context back to our generic retrieval context:
                //
                var genericRetrievalContexts = new List<IRetrievalContext>(retrievalResponse.Data.Count);
                foreach (var eriContext in retrievalResponse.Data)
                {
                    switch (eriContext.Type)
                    {
                        case ContentType.TEXT:
                            genericRetrievalContexts.Add(new RetrievalTextContext
                            {
                                Path = eriContext.Path ?? string.Empty,
                                Type = eriContext.ToRetrievalContentType(),
                                Links = eriContext.Links,
                                Category = eriContext.Type.ToRetrievalContentCategory(),
                                MatchedText = eriContext.MatchedContent,
                                DataSourceName = eriContext.Name,
                                SurroundingContent = eriContext.SurroundingContent,
                            });
                            break;
                        
                        case ContentType.IMAGE:
                            genericRetrievalContexts.Add(new RetrievalImageContext
                            {
                                Path = eriContext.Path ?? string.Empty,
                                Type = eriContext.ToRetrievalContentType(),
                                Links = eriContext.Links,
                                Source = eriContext.MatchedContent,
                                Category = eriContext.Type.ToRetrievalContentCategory(),
                                SourceType = ContentImageSource.BASE64,
                                DataSourceName = eriContext.Name,
                            });
                            break;
                        
                        default:
                            logger.LogWarning($"The ERI context type '{eriContext.Type}' is not supported yet.");
                            break;
                    }
                }

                return genericRetrievalContexts;
            }

            logger.LogWarning($"Was not able to retrieve data from the ERI data source '{this.Name}'. Message: {retrievalResponse.Message}");
            return [];
        }

        logger.LogWarning($"Was not able to authenticate with the ERI data source '{this.Name}'. Message: {authResponse.Message}");
        return [];
    }
}