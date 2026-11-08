using System.Text.Json;

namespace AIStudio.Assistants.BeyondFactsLab;

public sealed partial class BeyondFactsLabStore
{
    /// <summary>
    /// Gets the preparation the person selected last, or null when there is none or it does not exist anymore.
    /// </summary>
    public Guid? LastSelectedProjectId { get; private set; }

    /// <summary>
    /// Lists every preparation, also those this version cannot open, the one changed last first.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The preparations.</returns>
    public async Task<IReadOnlyList<BflProjectEntry>> ListProjectsAsync(CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        List<BflProjectEntry> projects = [];
        foreach (var directory in Directory.EnumerateDirectories(this.RootDirectory))
        {
            token.ThrowIfCancellationRequested();
            if (!Guid.TryParseExact(Path.GetFileName(directory), "D", out var projectId))
                continue;

            projects.Add(await this.LoadProjectEntryAsync(projectId, directory, token));
        }

        return [.. projects.OrderByDescending(project => project.ModifiedAtUtc)];
    }

    /// <summary>
    /// Loads a preparation.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The preparation, or null when it does not exist or this version cannot open it.</returns>
    public async Task<BflProject?> LoadAsync(Guid projectId, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        try
        {
            return await this.LoadWithoutInitializeAsync(projectId, token);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            this.LogUnavailableProject(projectId, exception);
            return null;
        }
    }

    /// <summary>
    /// Creates a preparation.
    /// </summary>
    /// <param name="name">The name the person gave it.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The new preparation.</returns>
    /// <exception cref="ArgumentException">The name is empty.</exception>
    public async Task<BflProject> CreateAsync(string name, CancellationToken token = default)
    {
        var sanitizedName = RequireName(name);
        await this.InitializeAsync(token);

        var projectId = Guid.NewGuid();
        var gate = this.GetLock(projectId);
        await gate.WaitAsync(token);
        try
        {
            var directory = this.ProjectDirectory(projectId);
            if (Directory.Exists(directory))
                throw new IOException($"A preparation with the ID '{projectId}' exists already.");

            var now = DateTimeOffset.UtcNow;
            var project = new BflProject
            {
                ProjectId = projectId,
                Name = sanitizedName,
                CreatedAtUtc = now,
                ModifiedAtUtc = now,
            };

            await this.StoreProjectAsync(project, token);
            return project;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Renames a preparation.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="name">The new name.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The renamed preparation.</returns>
    /// <exception cref="ArgumentException">The name is empty.</exception>
    public async Task<BflProject> RenameAsync(Guid projectId, string name, CancellationToken token = default)
    {
        var sanitizedName = RequireName(name);
        return await this.MutateProjectAsync(projectId, project => project.Name = sanitizedName, token);
    }

    /// <summary>
    /// Marks the event of a preparation as having taken place, or takes that back.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="happenedAtUtc">When the event took place, or null when it has not.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The changed preparation.</returns>
    public async Task<BflProject> SetHappenedAsync(Guid projectId, DateTimeOffset? happenedAtUtc, CancellationToken token = default)
    {
        return await this.MutateProjectAsync(projectId, project => project.HappenedAtUtc = happenedAtUtc?.ToUniversalTime(), token);
    }

    /// <summary>
    /// Deletes a preparation with everything it holds, also when this version cannot open it.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task DeleteAsync(Guid projectId, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var gate = this.GetLock(projectId);
        await gate.WaitAsync(token);
        try
        {
            var directory = this.ProjectDirectory(projectId);
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        finally
        {
            gate.Release();
            this.ForgetLock(projectId);
        }

        await this.ForgetSelectionAsync(projectId, token);
    }

    /// <summary>
    /// Remembers the preparation the person selected, for the next time the lab opens.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task RememberSelectionAsync(Guid projectId, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        if (this.LastSelectedProjectId == projectId)
            return;

        await this.selectionLock.WaitAsync(token);
        try
        {
            this.LastSelectedProjectId = projectId;
            await WriteTextAtomicAsync(this.SelectionPath(), JsonSerializer.Serialize<Guid?>(projectId, JSON_OPTIONS), token);
        }
        finally
        {
            this.selectionLock.Release();
        }
    }

    /// <summary>
    /// Forgets the selection when it names the given preparation.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="token">The cancellation token.</param>
    private async Task ForgetSelectionAsync(Guid projectId, CancellationToken token)
    {
        if (this.LastSelectedProjectId != projectId)
            return;

        await this.selectionLock.WaitAsync(token);
        try
        {
            if (this.LastSelectedProjectId != projectId)
                return;

            this.LastSelectedProjectId = null;
            await WriteTextAtomicAsync(this.SelectionPath(), JsonSerializer.Serialize<Guid?>(null, JSON_OPTIONS), token);
        }
        finally
        {
            this.selectionLock.Release();
        }
    }

    /// <summary>
    /// Reads the preparation selected last, when it still exists.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    private async Task LoadSelectionAsync(CancellationToken token)
    {
        var path = this.SelectionPath();
        if (!File.Exists(path))
            return;

        try
        {
            var serialized = await File.ReadAllTextAsync(path, token);
            var selected = JsonSerializer.Deserialize<Guid?>(serialized, JSON_OPTIONS);
            this.LastSelectedProjectId = selected is not null && Directory.Exists(this.ProjectDirectory(selected.Value)) ? selected : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(exception, "Could not read the preparation of the BeyondFacts Lab selected last.");
            this.LastSelectedProjectId = null;
        }
    }

    /// <summary>
    /// Loads a preparation for the list, or an entry which says why it cannot be opened.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="directory">The folder of the preparation.</param>
    /// <param name="token">The cancellation token.</param>
    private async Task<BflProjectEntry> LoadProjectEntryAsync(Guid projectId, string directory, CancellationToken token)
    {
        var path = this.ProjectPath(projectId);
        var fallbackModifiedAtUtc = LastWriteTimeUtc(path, directory);
        if (!File.Exists(path))
            return new(projectId, string.Empty, fallbackModifiedAtUtc, BflProjectLoadStatus.UNAVAILABLE, null);

        string json;
        try
        {
            json = await File.ReadAllTextAsync(path, token);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.LogUnavailableProject(projectId, exception);
            return new(projectId, string.Empty, fallbackModifiedAtUtc, BflProjectLoadStatus.UNAVAILABLE, null);
        }

        try
        {
            var project = JsonSerializer.Deserialize<BflProject>(json, JSON_OPTIONS);
            if (project is not null && IsValidProject(project, projectId))
                return BflProjectEntry.FromProject(project);
        }
        catch (JsonException exception)
        {
            this.LogUnavailableProject(projectId, exception);
        }

        return ReadRecoveryEntry(projectId, json, fallbackModifiedAtUtc);
    }

    /// <summary>
    /// Reads what the list can show about a preparation this version cannot open: its name, when it changed,
    /// and whether a newer version stored it.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="json">The content of its file.</param>
    /// <param name="fallbackModifiedAtUtc">When the file changed, for files without a readable date.</param>
    private static BflProjectEntry ReadRecoveryEntry(Guid projectId, string json, DateTimeOffset fallbackModifiedAtUtc)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind is not JsonValueKind.Object)
                return new(projectId, string.Empty, fallbackModifiedAtUtc, BflProjectLoadStatus.UNAVAILABLE, null);

            var name = root.TryGetProperty("name", out var nameElement) && nameElement.ValueKind is JsonValueKind.String ? SanitizeName(nameElement.GetString()) : string.Empty;
            var modifiedAtUtc = root.TryGetProperty("modifiedAtUtc", out var modifiedElement) && modifiedElement.ValueKind is JsonValueKind.String && modifiedElement.TryGetDateTimeOffset(out var parsedModifiedAtUtc)
                ? parsedModifiedAtUtc
                : fallbackModifiedAtUtc;

            var isNewer = root.TryGetProperty("schemaVersion", out var versionElement) && versionElement.ValueKind is JsonValueKind.Number && versionElement.TryGetInt32(out var schemaVersion) && schemaVersion > BflProject.CURRENT_SCHEMA_VERSION;
            return new(projectId, name, modifiedAtUtc, isNewer ? BflProjectLoadStatus.NEWER_VERSION : BflProjectLoadStatus.UNAVAILABLE, null);
        }
        catch (JsonException)
        {
            return new(projectId, string.Empty, fallbackModifiedAtUtc, BflProjectLoadStatus.UNAVAILABLE, null);
        }
    }

    /// <summary>
    /// Gets when the file of a preparation, or else its folder, changed last.
    /// </summary>
    /// <param name="projectPath">The file of the preparation.</param>
    /// <param name="directory">The folder of the preparation.</param>
    private static DateTimeOffset LastWriteTimeUtc(string projectPath, string directory)
    {
        try
        {
            var timestamp = File.Exists(projectPath) ? File.GetLastWriteTimeUtc(projectPath) : Directory.GetLastWriteTimeUtc(directory);
            return new DateTimeOffset(timestamp);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return DateTimeOffset.UnixEpoch;
        }
    }

    /// <summary>
    /// Changes a preparation under its lock and stores it.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="mutation">The change.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The changed preparation.</returns>
    /// <exception cref="FileNotFoundException">The preparation does not exist.</exception>
    /// <exception cref="InvalidDataException">This version cannot open the preparation, so it does not write it either.</exception>
    private async Task<BflProject> MutateProjectAsync(Guid projectId, Action<BflProject> mutation, CancellationToken token)
    {
        await this.InitializeAsync(token);
        var gate = this.GetLock(projectId);
        await gate.WaitAsync(token);
        try
        {
            var path = this.ProjectPath(projectId);
            if (!File.Exists(path))
                throw new FileNotFoundException("The preparation does not exist.", path);

            BflProject? project;
            try
            {
                project = await this.LoadWithoutInitializeAsync(projectId, token);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("This version of AI Studio cannot open the preparation.", exception);
            }

            if (project is null)
                throw new InvalidDataException("This version of AI Studio cannot open the preparation.");

            mutation(project);
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
    /// Reads a preparation without the first start of the store.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The preparation, or null when it does not exist or is not valid.</returns>
    private async Task<BflProject?> LoadWithoutInitializeAsync(Guid projectId, CancellationToken token)
    {
        var path = this.ProjectPath(projectId);
        if (!File.Exists(path))
            return null;

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, true);
        var project = await JsonSerializer.DeserializeAsync<BflProject>(stream, JSON_OPTIONS, token);
        return project is not null && IsValidProject(project, projectId) ? project : null;
    }

    /// <summary>
    /// Writes a preparation in the form of this version.
    /// </summary>
    /// <param name="project">The preparation.</param>
    /// <param name="token">The cancellation token.</param>
    private async Task StoreProjectAsync(BflProject project, CancellationToken token)
    {
        project.SchemaVersion = BflProject.CURRENT_SCHEMA_VERSION;
        var json = JsonSerializer.Serialize(project, JSON_OPTIONS);
        await WriteTextAtomicAsync(this.ProjectPath(project.ProjectId), json, token);
    }

    /// <summary>
    /// Checks a preparation which was read from a file.
    /// </summary>
    /// <param name="project">The preparation.</param>
    /// <param name="expectedProjectId">The ID its folder names.</param>
    private static bool IsValidProject(BflProject project, Guid expectedProjectId) =>
        project.SchemaVersion is >= 1 and <= BflProject.CURRENT_SCHEMA_VERSION &&
        project.ProjectId != Guid.Empty &&
        project.ProjectId == expectedProjectId &&
        !string.IsNullOrWhiteSpace(project.Name);

    /// <summary>
    /// Cleans a name and refuses one which is empty afterward.
    /// </summary>
    /// <param name="name">The name as entered.</param>
    /// <returns>The cleaned name.</returns>
    /// <exception cref="ArgumentException">The name is empty.</exception>
    private static string RequireName(string name)
    {
        var sanitizedName = SanitizeName(name);
        if (sanitizedName.Length == 0)
            throw new ArgumentException("A preparation needs a name.", nameof(name));

        return sanitizedName;
    }

    /// <summary>
    /// Logs why a preparation cannot be opened.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="exception">What went wrong.</param>
    private void LogUnavailableProject(Guid projectId, Exception exception)
    {
        logger.LogWarning(exception, "Could not open the preparation '{ProjectId}' of the BeyondFacts Lab.", projectId);
    }
}