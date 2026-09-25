using System.Collections.Concurrent;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ERIClient;
using AIStudio.Tools.Security;

namespace AIStudio.Tools.Services;

/// <summary>
/// Tells a model what a data source holds, so that it can decide where to search.
/// </summary>
/// <remarks>
/// Both the agent which selects data sources and Semantic Search describe the data sources to a
/// model. The user describes a local data source. An ERI data source is described by its server,
/// which costs two requests and is written by somebody else: that description is filtered for
/// prompt injections like any other external content before it is kept. It is kept for a few
/// minutes, because Semantic Search describes the data sources with every request it makes.
/// </remarks>
public sealed class DataSourceDescriptionService(RustService rustService, PromptInjectionGuardService guardService, ILogger<DataSourceDescriptionService> logger)
{
    private static readonly TimeSpan SERVER_DESCRIPTION_LIFETIME = TimeSpan.FromMinutes(5);

    // As long as the check of its security requirements may take, cf. DataSourceService:
    private static readonly TimeSpan SERVER_TIMEOUT = TimeSpan.FromSeconds(6);

    /// <summary>
    /// A description as the server sent it, filtered, together with the configuration it was asked with.
    /// </summary>
    private readonly record struct ServerDescription(IERIDataSource DataSource, string Description, DateTimeOffset ValidUntil);

    private readonly ConcurrentDictionary<string, ServerDescription> serverDescriptions = new(StringComparer.Ordinal);

    /// <summary>
    /// What the data source holds, in a single line.
    /// </summary>
    /// <param name="dataSource">The data source to describe.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The description, or an empty string when there is none or its server could not be asked.</returns>
    public async Task<string> GetDescriptionAsync(IDataSource dataSource, CancellationToken token = default)
    {
        var description = dataSource switch
        {
            DataSourceLocalDirectory localDirectory => localDirectory.Description,
            DataSourceLocalFile localFile => localFile.Description,
            IERIDataSource eriDataSource => await this.GetServerDescriptionAsync(eriDataSource, token),
            _ => string.Empty,
        };

        // A description is written into a list, one data source per line:
        return description.Replace("\n", " ").Replace("\r", " ");
    }

    private async Task<string> GetServerDescriptionAsync(IERIDataSource dataSource, CancellationToken token)
    {
        //
        // A changed configuration, e.g., another server or another account, asks anew rather than
        // waiting for the old description to expire:
        //
        if (this.serverDescriptions.TryGetValue(dataSource.Id, out var known) && known.DataSource.Equals(dataSource) && known.ValidUntil > DateTimeOffset.UtcNow)
            return known.Description;

        var description = await this.FetchServerDescriptionAsync(dataSource, token);
        if (description is null)
            return string.Empty;

        //
        // Only an answer is kept. A server which gave none is asked again next time; it is rarely
        // asked at all, since a data source whose server cannot be reached is not offered anyway.
        //
        this.serverDescriptions[dataSource.Id] = new(dataSource, description, DateTimeOffset.UtcNow + SERVER_DESCRIPTION_LIFETIME);
        return description;
    }

    /// <returns>The filtered description, or null when the server could not be asked.</returns>
    private async Task<string?> FetchServerDescriptionAsync(IERIDataSource dataSource, CancellationToken token)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(SERVER_TIMEOUT);

            using var eriClient = ERIClientFactory.Get(dataSource.Version, dataSource);
            if (eriClient is null)
            {
                logger.LogWarning($"Could not create an ERI client for the data source '{dataSource.Name}'. Thus, we cannot retrieve the server description.");
                return null;
            }

            var authResponse = await eriClient.AuthenticateAsync(rustService, cancellationToken: timeout.Token);
            if (!authResponse.Successful)
            {
                logger.LogWarning($"Was not able to authenticate with the ERI data source '{dataSource.Name}'. Message: {authResponse.Message}");
                return null;
            }

            var serverDescriptionResponse = await eriClient.GetDataSourceInfoAsync(timeout.Token);
            if (!serverDescriptionResponse.Successful)
            {
                logger.LogWarning($"Was not able to retrieve the server description from the ERI data source '{dataSource.Name}'. Message: {serverDescriptionResponse.Message}");
                return null;
            }

            //
            // Whoever runs the server writes this, and a model reads it as the description of
            // where to search -- a fine place to tell it what to do instead:
            //
            return await guardService.SanitizeAsync(serverDescriptionResponse.Data.Description, PromptInjectionSource.DataSourceDescription(dataSource.Name));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.LogWarning($"The ERI data source '{dataSource.Name}' is not available. Thus, we cannot retrieve the server description. Error: {e.Message}");
            return null;
        }
    }
}