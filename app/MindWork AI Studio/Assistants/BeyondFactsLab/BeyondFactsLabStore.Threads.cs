using System.Text.Json;

using AIStudio.Chat;

namespace AIStudio.Assistants.BeyondFactsLab;

public sealed partial class BeyondFactsLabStore
{
    /// <summary>
    /// The file which holds a chat of the lab.
    /// </summary>
    private const string THREAD_FILE_NAME = "thread.json";

    /// <summary>
    /// The folder of the conversation about whether the lab fits a task. It is no ID, so no preparation can have it.
    /// </summary>
    private const string ONBOARDING_DIRECTORY_NAME = "onboarding";

    /// <summary>
    /// Guards the conversation about whether the lab fits a task, which belongs to no preparation.
    /// </summary>
    private readonly SemaphoreSlim onboardingLock = new(1, 1);

    /// <summary>
    /// Loads a chat of the lab.
    /// </summary>
    /// <param name="location">Where the chat belongs.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The chat, or null while there is none.</returns>
    /// <exception cref="InvalidDataException">This version cannot open the chat.</exception>
    public async Task<ChatThread?> LoadThreadAsync(BflThreadLocation location, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        return await this.LoadThreadWithoutInitializeAsync(location, token);
    }

    /// <summary>
    /// Stores a chat of the lab. The chat of a step counts as a change of its preparation.
    /// </summary>
    /// <param name="location">Where the chat belongs.</param>
    /// <param name="thread">The chat.</param>
    /// <param name="token">The cancellation token.</param>
    /// <exception cref="FileNotFoundException">The preparation of the step does not exist, e.g., because the person deleted it while the model answered.</exception>
    /// <exception cref="InvalidDataException">This version cannot open the preparation or the stored chat, so it does not write them either.</exception>
    public async Task SaveThreadAsync(BflThreadLocation location, ChatThread thread, CancellationToken token = default)
    {
        if (thread.ChatId != location.ChatId)
            throw new ArgumentException("The chat belongs to another place.", nameof(thread));

        await this.InitializeAsync(token);
        var gate = this.ThreadLock(location);
        await gate.WaitAsync(token);
        try
        {
            var project = location is BflStepThreadLocation stepLocation ? await this.LoadRequiredWithoutInitializeAsync(stepLocation.ProjectId, token) : null;

            // A chat which a newer version stored, or which is damaged, stays as it is:
            await this.LoadThreadWithoutInitializeAsync(location, token);

            await WriteTextAtomicAsync(this.ThreadPath(location), JsonSerializer.Serialize(thread, WorkspaceBehaviour.JSON_OPTIONS), token);
            if (project is null)
                return;

            project.ModifiedAtUtc = DateTimeOffset.UtcNow;
            await this.StoreProjectAsync(project, token);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Deletes a chat of the lab, so that its conversation starts over.
    /// </summary>
    /// <remarks>
    /// Unlike the other files of a step, a chat which this version cannot open is deleted as well: the person asked
    /// to start the conversation over, and nothing else refers to it.
    /// </remarks>
    /// <param name="location">Where the chat belongs.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task DeleteThreadAsync(BflThreadLocation location, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var gate = this.ThreadLock(location);
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
    /// Finds every chat of the lab on the disk, from the folders alone.
    /// </summary>
    /// <remarks>
    /// For the cleanup of the provenance store at the start, which must know every chat which may refer to an entry,
    /// also when the lab is switched off. A preparation this version cannot open keeps its chats as well.
    /// </remarks>
    /// <returns>The place of each chat, by the ID of the chat.</returns>
    public IReadOnlyDictionary<Guid, BflThreadLocation> ReadAllThreadLocations()
    {
        var locations = new Dictionary<Guid, BflThreadLocation>();
        if (!Directory.Exists(this.RootDirectory))
            return locations;

        AddWhenStored(BflOnboardingThreadLocation.INSTANCE);
        foreach (var directory in Directory.EnumerateDirectories(this.RootDirectory))
        {
            if (!Guid.TryParse(Path.GetFileName(directory), out var projectId))
                continue;

            foreach (var step in BflStep.All)
                AddWhenStored(new BflStepThreadLocation(projectId, step));
        }

        return locations;

        void AddWhenStored(BflThreadLocation location)
        {
            if (File.Exists(this.ThreadPath(location)))
                locations[location.ChatId] = location;
        }
    }

    /// <summary>
    /// Tells when a chat of the lab was last written.
    /// </summary>
    /// <param name="location">Where the chat belongs.</param>
    /// <returns>The time, or null when there is no chat.</returns>
    public DateTimeOffset? GetThreadWrittenAt(BflThreadLocation location)
    {
        var path = this.ThreadPath(location);
        return File.Exists(path) ? new DateTimeOffset(File.GetLastWriteTimeUtc(path)) : null;
    }

    /// <summary>
    /// Reads a chat of the lab without the first start of the store.
    /// </summary>
    /// <param name="location">Where the chat belongs.</param>
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

        // A chat copied into another place would mix up what the provenance store keeps for it:
        if (thread is null || thread.ChatId != location.ChatId || IsNull(thread.Blocks))
            throw new InvalidDataException("This version of AI Studio cannot open the chat.");

        return thread;
    }

    /// <summary>
    /// Gets the file of a chat of the lab.
    /// </summary>
    /// <param name="location">Where the chat belongs.</param>
    private string ThreadPath(BflThreadLocation location) => location switch
    {
        BflStepThreadLocation stepLocation => Path.Combine(this.ProjectDirectory(stepLocation.ProjectId), STEPS_DIRECTORY_NAME, stepLocation.Step.FolderName, THREAD_FILE_NAME),
        BflOnboardingThreadLocation => Path.Combine(this.RootDirectory, ONBOARDING_DIRECTORY_NAME, THREAD_FILE_NAME),

        _ => throw new ArgumentOutOfRangeException(nameof(location), location, "The lab keeps no chat there.")
    };

    /// <summary>
    /// Gets the lock which guards a chat of the lab: the chat of a step shares the lock of its preparation.
    /// </summary>
    /// <param name="location">Where the chat belongs.</param>
    private SemaphoreSlim ThreadLock(BflThreadLocation location) => location switch
    {
        BflStepThreadLocation stepLocation => this.GetLock(stepLocation.ProjectId),
        BflOnboardingThreadLocation => this.onboardingLock,

        _ => throw new ArgumentOutOfRangeException(nameof(location), location, "The lab keeps no chat there.")
    };
}