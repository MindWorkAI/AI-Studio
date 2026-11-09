using System.Text.Json;

using AIStudio.Chat;

namespace AIStudio.Assistants.BeyondFactsLab;

public sealed partial class BeyondFactsLabStore
{
    /// <summary>
    /// The file of a step which holds its chat.
    /// </summary>
    private const string THREAD_FILE_NAME = "thread.json";

    /// <summary>
    /// Loads the chat of a step.
    /// </summary>
    /// <param name="location">The step of the preparation.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The chat, or null while the step has none.</returns>
    /// <exception cref="InvalidDataException">This version cannot open the chat.</exception>
    public async Task<ChatThread?> LoadThreadAsync(BflThreadLocation location, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        return await this.LoadThreadWithoutInitializeAsync(location, token);
    }

    /// <summary>
    /// Stores the chat of a step and counts it as a change of the preparation.
    /// </summary>
    /// <param name="location">The step of the preparation.</param>
    /// <param name="thread">The chat.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The preparation with its new time of change.</returns>
    /// <exception cref="FileNotFoundException">The preparation does not exist, e.g., because the person deleted it while the model answered.</exception>
    /// <exception cref="InvalidDataException">This version cannot open the preparation or the stored chat, so it does not write them either.</exception>
    public async Task<BflProject> SaveThreadAsync(BflThreadLocation location, ChatThread thread, CancellationToken token = default)
    {
        if (thread.ChatId != location.ChatId)
            throw new ArgumentException("The chat belongs to another step.", nameof(thread));

        await this.InitializeAsync(token);
        var gate = this.GetLock(location.ProjectId);
        await gate.WaitAsync(token);
        try
        {
            var project = await this.LoadRequiredWithoutInitializeAsync(location.ProjectId, token);

            // A chat which a newer version stored, or which is damaged, stays as it is:
            await this.LoadThreadWithoutInitializeAsync(location, token);

            await WriteTextAtomicAsync(this.ThreadPath(location), JsonSerializer.Serialize(thread, WorkspaceBehaviour.JSON_OPTIONS), token);

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
    /// Deletes the chat of a step, so that its conversation starts over.
    /// </summary>
    /// <remarks>
    /// Unlike the other files of a step, a chat which this version cannot open is deleted as well: the person asked
    /// to start the conversation over, and nothing else refers to it.
    /// </remarks>
    /// <param name="location">The step of the preparation.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task DeleteThreadAsync(BflThreadLocation location, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var gate = this.GetLock(location.ProjectId);
        await gate.WaitAsync(token);
        try
        {
            var path = this.ThreadPath(location);
            if (File.Exists(path))
                File.Delete(path);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Finds the chats of every preparation on the disk, from the folders alone.
    /// </summary>
    /// <remarks>
    /// For the cleanup of the provenance store at the start, which must know every chat which may refer to an entry,
    /// also when the lab is switched off. A preparation this version cannot open keeps its chats as well.
    /// </remarks>
    /// <returns>The step of each chat, by the ID of the chat.</returns>
    public IReadOnlyDictionary<Guid, BflThreadLocation> ReadAllThreadLocations()
    {
        var locations = new Dictionary<Guid, BflThreadLocation>();
        if (!Directory.Exists(this.RootDirectory))
            return locations;

        foreach (var directory in Directory.EnumerateDirectories(this.RootDirectory))
        {
            if (!Guid.TryParse(Path.GetFileName(directory), out var projectId))
                continue;

            foreach (var step in BflStep.All)
            {
                var location = new BflThreadLocation(projectId, step);
                if (File.Exists(this.ThreadPath(location)))
                    locations[location.ChatId] = location;
            }
        }

        return locations;
    }

    /// <summary>
    /// Tells when the chat of a step was last written.
    /// </summary>
    /// <param name="location">The step of the preparation.</param>
    /// <returns>The time, or null when the step has no chat.</returns>
    public DateTimeOffset? GetThreadWrittenAt(BflThreadLocation location)
    {
        var path = this.ThreadPath(location);
        return File.Exists(path) ? new DateTimeOffset(File.GetLastWriteTimeUtc(path)) : null;
    }

    /// <summary>
    /// Reads the chat of a step without the first start of the store.
    /// </summary>
    /// <param name="location">The step of the preparation.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The chat, or null when there is no file.</returns>
    /// <exception cref="InvalidDataException">This version cannot open the chat.</exception>
    private async Task<ChatThread?> LoadThreadWithoutInitializeAsync(BflThreadLocation location, CancellationToken token)
    {
        var path = this.ThreadPath(location);
        if (!File.Exists(path))
            return null;

        ChatThread? thread;
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, true);
            thread = await JsonSerializer.DeserializeAsync<ChatThread>(stream, WorkspaceBehaviour.JSON_OPTIONS, token);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new InvalidDataException("This version of AI Studio cannot open the chat.", exception);
        }

        // A chat copied into another step would mix up what the provenance store keeps for it:
        if (thread is null || thread.ChatId != location.ChatId || IsNull(thread.Blocks))
            throw new InvalidDataException("This version of AI Studio cannot open the chat.");

        return thread;
    }

    /// <summary>
    /// Gets the file of the chat of a step.
    /// </summary>
    /// <param name="location">The step of the preparation.</param>
    private string ThreadPath(BflThreadLocation location) => Path.Combine(this.ProjectDirectory(location.ProjectId), STEPS_DIRECTORY_NAME, location.Step.FolderName, THREAD_FILE_NAME);
}