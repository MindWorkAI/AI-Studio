using System.Text.Json;

namespace AIStudio.Assistants.BeyondFactsLab;

public sealed partial class BeyondFactsLabStore
{
    /// <summary>
    /// The folder of the step "Strategy" which holds the research, one file for each topic.
    /// </summary>
    private const string RESEARCH_DIRECTORY_NAME = "research";

    /// <summary>
    /// Loads the research of a preparation on one topic.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="topic">The topic of the research.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The research, or null while there is none.</returns>
    /// <exception cref="InvalidDataException">This version cannot open the research.</exception>
    public async Task<BflResearch?> LoadResearchAsync(Guid projectId, BflResearchTopic topic, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        return await this.LoadResearchWithoutInitializeAsync(projectId, topic, token);
    }

    /// <summary>
    /// Loads the research of a preparation on every topic.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The research this version can open, by its topics; a topic without research is missing.</returns>
    public async Task<IReadOnlyDictionary<BflResearchTopic, BflResearch>> LoadAllResearchAsync(Guid projectId, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var research = new Dictionary<BflResearchTopic, BflResearch>();
        foreach (var topic in BflResearchTopic.All)
        {
            try
            {
                if (await this.LoadResearchWithoutInitializeAsync(projectId, topic, token) is { } found)
                    research[topic] = found;
            }
            catch (InvalidDataException exception)
            {
                logger.LogWarning(exception, "Could not open the research '{Topic}' of the preparation '{ProjectId}' of the BeyondFacts Lab.", topic, projectId);
            }
        }

        return research;
    }

    /// <summary>
    /// Stores the research of a preparation on one topic, replacing an earlier one, and counts it as a change of the
    /// preparation.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="topic">The topic of the research.</param>
    /// <param name="research">The research.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The preparation with its new time of change.</returns>
    /// <exception cref="FileNotFoundException">The preparation does not exist.</exception>
    /// <exception cref="InvalidDataException">This version cannot open the preparation or the stored research, so it does not write them either.</exception>
    public async Task<BflProject> SaveResearchAsync(Guid projectId, BflResearchTopic topic, BflResearch research, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var gate = this.GetLock(projectId);
        await gate.WaitAsync(token);
        try
        {
            var project = await this.LoadRequiredWithoutInitializeAsync(projectId, token);

            // A research which a newer version stored, or which is damaged, stays as it is:
            await this.LoadResearchWithoutInitializeAsync(projectId, topic, token);

            research.SchemaVersion = BflResearch.CURRENT_SCHEMA_VERSION;
            await WriteTextAtomicAsync(this.ResearchPath(projectId, topic), JsonSerializer.Serialize(research, JSON_OPTIONS), token);

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
    /// Reads the research of a preparation on one topic without the first start of the store.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="topic">The topic of the research.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The research, or null when there is no file.</returns>
    /// <exception cref="InvalidDataException">This version cannot open the research.</exception>
    private async Task<BflResearch?> LoadResearchWithoutInitializeAsync(Guid projectId, BflResearchTopic topic, CancellationToken token)
    {
        var path = this.ResearchPath(projectId, topic);
        if (!File.Exists(path))
            return null;

        BflResearch? research;
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, true);
            research = await JsonSerializer.DeserializeAsync<BflResearch>(stream, JSON_OPTIONS, token);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new InvalidDataException("This version of AI Studio cannot open the research.", exception);
        }

        if (research is null || research.SchemaVersion is < 1 or > BflResearch.CURRENT_SCHEMA_VERSION || IsNull(research.Text) || IsNull(research.Details) || IsNull(research.SearchQueries) || IsNull(research.Pages))
            throw new InvalidDataException("This version of AI Studio cannot open the research.");

        return research;
    }

    /// <summary>
    /// Gets the file of the research of a preparation on one topic.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="topic">The topic of the research.</param>
    private string ResearchPath(Guid projectId, BflResearchTopic topic) => Path.Combine(this.ProjectDirectory(projectId), STEPS_DIRECTORY_NAME, BflStep.STRATEGY.FolderName, RESEARCH_DIRECTORY_NAME, topic.FileName);
}