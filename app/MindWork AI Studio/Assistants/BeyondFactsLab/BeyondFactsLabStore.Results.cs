using System.Text.Json;

namespace AIStudio.Assistants.BeyondFactsLab;

public sealed partial class BeyondFactsLabStore
{
    /// <summary>
    /// The folder of a preparation which holds its results, one file for each kind.
    /// </summary>
    private const string RESULTS_DIRECTORY_NAME = "results";

    /// <summary>
    /// Loads a result of a preparation.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="kind">The kind of result.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The result, or an empty one while the preparation has none.</returns>
    /// <exception cref="InvalidDataException">This version cannot open the result.</exception>
    public async Task<BflResult> LoadResultAsync(Guid projectId, BflResultKind kind, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        return await this.LoadResultWithoutInitializeAsync(projectId, kind, token);
    }

    /// <summary>
    /// Loads every result of a preparation.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The results this version can open, by their kinds.</returns>
    public async Task<IReadOnlyDictionary<BflResultKind, BflResult>> LoadResultsAsync(Guid projectId, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var results = new Dictionary<BflResultKind, BflResult>();
        foreach (var kind in BflResultKind.All)
        {
            try
            {
                results[kind] = await this.LoadResultWithoutInitializeAsync(projectId, kind, token);
            }
            catch (InvalidDataException exception)
            {
                logger.LogWarning(exception, "Could not open the result '{Kind}' of the preparation '{ProjectId}' of the BeyondFacts Lab.", kind, projectId);
            }
        }

        return results;
    }

    /// <summary>
    /// Stores a result of a preparation and counts it as a change of the preparation.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="kind">The kind of result.</param>
    /// <param name="result">The result.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The preparation with its new time of change.</returns>
    /// <exception cref="FileNotFoundException">The preparation does not exist.</exception>
    /// <exception cref="InvalidDataException">This version cannot open the preparation or the stored result, so it does not write them either.</exception>
    public async Task<BflProject> SaveResultAsync(Guid projectId, BflResultKind kind, BflResult result, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var gate = this.GetLock(projectId);
        await gate.WaitAsync(token);
        try
        {
            var project = await this.LoadRequiredWithoutInitializeAsync(projectId, token);

            // A result which a newer version stored, or which is damaged, stays as it is:
            await this.LoadResultWithoutInitializeAsync(projectId, kind, token);

            result.SchemaVersion = BflResult.CURRENT_SCHEMA_VERSION;
            await WriteTextAtomicAsync(this.ResultPath(projectId, kind), JsonSerializer.Serialize(result, JSON_OPTIONS), token);

            project.ModifiedAtUtc = DateTimeOffset.UtcNow;
            await this.StoreProjectAsync(project, token);
            return project;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Reads a result of a preparation without the first start of the store.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="kind">The kind of result.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The result, or an empty one when there is no file.</returns>
    /// <exception cref="InvalidDataException">This version cannot open the result.</exception>
    private async Task<BflResult> LoadResultWithoutInitializeAsync(Guid projectId, BflResultKind kind, CancellationToken token)
    {
        var path = this.ResultPath(projectId, kind);
        if (!File.Exists(path))
            return new BflResult();

        BflResult? result;
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, true);
            result = await JsonSerializer.DeserializeAsync<BflResult>(stream, JSON_OPTIONS, token);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new InvalidDataException("This version of AI Studio cannot open the result.", exception);
        }

        if (result is null || result.SchemaVersion is < 1 or > BflResult.CURRENT_SCHEMA_VERSION || !IsReadable(result.Draft) || !IsReadable(result.Confirmed))
            throw new InvalidDataException("This version of AI Studio cannot open the result.");

        return result;
    }

    /// <summary>
    /// Tells whether a version of a result, which may be missing, has everything this version of AI Studio reads.
    /// </summary>
    /// <param name="version">The version, or null.</param>
    private static bool IsReadable(BflResultVersion? version) => version is null || !IsNull(version.Text) && !IsNull(version.WorkStateRevisions);

    /// <summary>
    /// Gets the file of a result of a preparation.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="kind">The kind of result.</param>
    private string ResultPath(Guid projectId, BflResultKind kind) => Path.Combine(this.ProjectDirectory(projectId), RESULTS_DIRECTORY_NAME, kind.FileName);
}