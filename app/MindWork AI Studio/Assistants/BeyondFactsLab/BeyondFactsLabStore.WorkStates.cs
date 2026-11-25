using System.Text.Json;

namespace AIStudio.Assistants.BeyondFactsLab;

public sealed partial class BeyondFactsLabStore
{
    /// <summary>
    /// The file of a step which holds its work state.
    /// </summary>
    private const string WORK_STATE_FILE_NAME = "work-state.json";

    /// <summary>
    /// Loads the work state of a step.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="step">The step.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The work state, or an empty one while the step has none.</returns>
    /// <exception cref="InvalidDataException">This version cannot open the work state.</exception>
    public async Task<BflWorkState> LoadWorkStateAsync(Guid projectId, BflStep step, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        return await this.LoadWorkStateWithoutInitializeAsync(projectId, step, token);
    }

    /// <summary>
    /// Loads the work states of every step of a preparation, e.g., to tell which ones are outdated.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The work states this version can open, by their steps.</returns>
    public async Task<IReadOnlyDictionary<BflStep, BflWorkState>> LoadWorkStatesAsync(Guid projectId, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var workStates = new Dictionary<BflStep, BflWorkState>();
        foreach (var step in BflStep.All)
        {
            try
            {
                workStates[step] = await this.LoadWorkStateWithoutInitializeAsync(projectId, step, token);
            }
            catch (InvalidDataException exception)
            {
                logger.LogWarning(exception, "Could not open the work state of the step '{Step}' of the preparation '{ProjectId}' of the BeyondFacts Lab.", step, projectId);
            }
        }

        return workStates;
    }

    /// <summary>
    /// Stores the work state of a step and counts it as a change of the preparation.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="step">The step.</param>
    /// <param name="workState">The work state.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The preparation with its new time of change.</returns>
    /// <exception cref="FileNotFoundException">The preparation does not exist.</exception>
    /// <exception cref="InvalidDataException">This version cannot open the preparation or the stored work state, so it does not write them either.</exception>
    public async Task<BflProject> SaveWorkStateAsync(Guid projectId, BflStep step, BflWorkState workState, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var gate = this.GetLock(projectId);
        await gate.WaitAsync(token);
        try
        {
            var project = await this.LoadRequiredWithoutInitializeAsync(projectId, token);

            // A work state which a newer version stored, or which is damaged, stays as it is:
            await this.LoadWorkStateWithoutInitializeAsync(projectId, step, token);

            workState.SchemaVersion = BflWorkState.CURRENT_SCHEMA_VERSION;
            await WriteTextAtomicAsync(this.WorkStatePath(projectId, step), JsonSerializer.Serialize(workState, JSON_OPTIONS), token);

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
    /// Reads the work state of a step without the first start of the store.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="step">The step.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The work state, or an empty one when there is no file.</returns>
    /// <exception cref="InvalidDataException">This version cannot open the work state.</exception>
    private async Task<BflWorkState> LoadWorkStateWithoutInitializeAsync(Guid projectId, BflStep step, CancellationToken token)
    {
        var path = this.WorkStatePath(projectId, step);
        if (!File.Exists(path))
            return new BflWorkState();

        BflWorkState? workState;
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, true);
            workState = await JsonSerializer.DeserializeAsync<BflWorkState>(stream, JSON_OPTIONS, token);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new InvalidDataException("This version of AI Studio cannot open the work state.", exception);
        }

        if (workState is null || workState.SchemaVersion is < 1 or > BflWorkState.CURRENT_SCHEMA_VERSION || IsNull(workState.FieldSuggestions) || workState.Confirmed is { } confirmed && (IsNull(confirmed.Sections) || IsNull(confirmed.UpstreamRevisions) || IsNull(confirmed.DetailsFingerprint)))
            throw new InvalidDataException("This version of AI Studio cannot open the work state.");

        return workState;
    }

    /// <summary>
    /// Gets the file of the work state of a step.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="step">The step.</param>
    private string WorkStatePath(Guid projectId, BflStep step) => Path.Combine(this.ProjectDirectory(projectId), STEPS_DIRECTORY_NAME, step.FolderName, WORK_STATE_FILE_NAME);
}