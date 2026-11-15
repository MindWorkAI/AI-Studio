using System.Text.Json;

namespace AIStudio.Assistants.BeyondFactsLab;

public sealed partial class BeyondFactsLabStore
{
    /// <summary>
    /// The folder below the research of the step "Strategy" which holds the research on single people.
    /// </summary>
    private const string PERSON_RESEARCH_DIRECTORY_NAME = "participants";

    /// <summary>
    /// Loads the research on one person of a preparation.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="name">The name of the person.</param>
    /// <param name="token">Cancels the loading.</param>
    /// <returns>The research, or null when the person was not researched yet.</returns>
    /// <exception cref="InvalidDataException">The research is damaged, or a newer version of AI Studio stored it.</exception>
    public async Task<BflPersonResearch?> LoadPersonResearchAsync(Guid projectId, string name, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        return await this.LoadPersonResearchWithoutInitializeAsync(this.PersonResearchPath(projectId, BflPersonId.Of(name)), token);
    }

    /// <summary>
    /// Loads the research on every person of a preparation, including people who are no longer on the list of
    /// participants. A research which cannot be opened is left out and stays as it is.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="token">Cancels the loading.</param>
    /// <returns>The research on the people, in no particular order.</returns>
    public async Task<IReadOnlyList<BflPersonResearch>> LoadAllPersonResearchAsync(Guid projectId, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var directory = this.PersonResearchDirectory(projectId);
        if (!Directory.Exists(directory))
            return [];

        var research = new List<BflPersonResearch>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            // Only the files of people count, named by their IDs:
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "D", out _))
                continue;

            try
            {
                if (await this.LoadPersonResearchWithoutInitializeAsync(path, token) is { } found)
                    research.Add(found);
            }
            catch (InvalidDataException exception)
            {
                logger.LogWarning(exception, "Could not open the research on a person of the preparation '{ProjectId}' of the BeyondFacts Lab.", projectId);
            }
        }

        return research;
    }

    /// <summary>
    /// Stores the research on one person of a preparation, in place of the earlier one.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="research">The research.</param>
    /// <param name="token">Cancels the storing.</param>
    /// <returns>The preparation, with the time of its last change.</returns>
    /// <exception cref="FileNotFoundException">The preparation does not exist.</exception>
    /// <exception cref="InvalidDataException">The earlier research is damaged, or a newer version of AI Studio stored it; it stays as it is.</exception>
    public async Task<BflProject> SavePersonResearchAsync(Guid projectId, BflPersonResearch research, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var gate = this.GetLock(projectId);
        await gate.WaitAsync(token);
        try
        {
            var project = await this.LoadRequiredWithoutInitializeAsync(projectId, token);
            var path = this.PersonResearchPath(projectId, BflPersonId.Of(research.Name));

            // A research which a newer version stored, or which is damaged, stays as it is:
            await this.LoadPersonResearchWithoutInitializeAsync(path, token);

            research.SchemaVersion = BflPersonResearch.CURRENT_SCHEMA_VERSION;
            foreach (var stage in research.Stages)
                stage.Research.SchemaVersion = BflResearch.CURRENT_SCHEMA_VERSION;

            await WriteTextAtomicAsync(path, JsonSerializer.Serialize(research, JSON_OPTIONS), token);

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
    /// Loads the research on one person from its file.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="token">Cancels the loading.</param>
    /// <returns>The research, or null when the file does not exist.</returns>
    /// <exception cref="InvalidDataException">The research is damaged, belongs to another file, or a newer version of AI Studio stored it.</exception>
    private async Task<BflPersonResearch?> LoadPersonResearchWithoutInitializeAsync(string path, CancellationToken token)
    {
        if (!File.Exists(path))
            return null;

        BflPersonResearch? research;
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, true);
            research = await JsonSerializer.DeserializeAsync<BflPersonResearch>(stream, JSON_OPTIONS, token);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new InvalidDataException("This version of AI Studio cannot open the research on a person.", exception);
        }

        if (research is null || research.SchemaVersion is < 1 or > BflPersonResearch.CURRENT_SCHEMA_VERSION || IsNull(research.Name) || IsNull(research.Stages) || research.Stages.Any(stage => IsNull(stage) || IsNull(stage.Research) || IsBroken(stage.Research)))
            throw new InvalidDataException("This version of AI Studio cannot open the research on a person.");

        // The file of a person is named by the ID of their name; a research under another name is no research of theirs:
        if (!string.Equals(Path.GetFileNameWithoutExtension(path), BflPersonId.Of(research.Name).ToString("D"), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The research on a person is stored under the name of another person.");

        return research;
    }

    /// <summary>
    /// Gets the folder with the research on the people of a preparation.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    private string PersonResearchDirectory(Guid projectId) => Path.Combine(this.ProjectDirectory(projectId), STEPS_DIRECTORY_NAME, BflStep.STRATEGY.FolderName, RESEARCH_DIRECTORY_NAME, PERSON_RESEARCH_DIRECTORY_NAME);

    /// <summary>
    /// Gets the file with the research on one person.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="personId">The ID of the person, see BflPersonId.</param>
    private string PersonResearchPath(Guid projectId, Guid personId) => Path.Combine(this.PersonResearchDirectory(projectId), $"{personId:D}.json");
}