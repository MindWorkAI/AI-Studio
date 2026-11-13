using System.Text.Json;

using AIStudio.Chat;

namespace AIStudio.Assistants.ModelComparison;

public sealed partial class ModelComparisonStore
{
    /// <summary>
    /// The folder of the comparison series, one folder per series.
    /// </summary>
    private const string SERIES_DIRECTORY_NAME = "series";

    /// <summary>
    /// The file of a series in its folder.
    /// </summary>
    private const string SERIES_FILE_NAME = "series.json";

    /// <summary>
    /// The folder of the chats in the folder of a series.
    /// </summary>
    private const string CHATS_DIRECTORY_NAME = "chats";

    /// <summary>
    /// Gets the folder of all comparison series.
    /// </summary>
    private string AllSeriesDirectory => Path.Combine(this.RootDirectory, SERIES_DIRECTORY_NAME);

    /// <summary>
    /// Gets the folder of a series.
    /// </summary>
    /// <param name="seriesId">The ID of the series.</param>
    private string SeriesDirectory(Guid seriesId) => Path.Combine(this.AllSeriesDirectory, seriesId.ToString("D"));

    /// <summary>
    /// Gets the file of a series.
    /// </summary>
    /// <param name="seriesId">The ID of the series.</param>
    private string SeriesPath(Guid seriesId) => Path.Combine(this.SeriesDirectory(seriesId), SERIES_FILE_NAME);

    /// <summary>
    /// Gets the file of a chat of a series.
    /// </summary>
    /// <param name="seriesId">The ID of the series.</param>
    /// <param name="chatId">The ID of the chat.</param>
    private string SeriesChatPath(Guid seriesId, Guid chatId) => Path.Combine(this.SeriesDirectory(seriesId), CHATS_DIRECTORY_NAME, $"{chatId:D}.json");

    /// <summary>
    /// Lists the comparison series which this version can read, the one the user last worked on first.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The series.</returns>
    public async Task<IReadOnlyList<ModelComparisonSeries>> ListSeriesAsync(CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        if (!Directory.Exists(this.AllSeriesDirectory))
            return [];

        List<ModelComparisonSeries> series = [];
        foreach (var directory in Directory.EnumerateDirectories(this.AllSeriesDirectory))
        {
            token.ThrowIfCancellationRequested();
            if (Guid.TryParseExact(Path.GetFileName(directory), "D", out var seriesId) && await this.LoadSeriesAsync(seriesId, token) is { } loaded)
                series.Add(loaded);
        }

        return [.. series.OrderByDescending(entry => entry.LastActivityUtc)];
    }

    /// <summary>
    /// Loads a comparison series.
    /// </summary>
    /// <param name="seriesId">The ID of the series.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The series, or null when it does not exist or this version cannot read it.</returns>
    public async Task<ModelComparisonSeries?> LoadSeriesAsync(Guid seriesId, CancellationToken token = default)
    {
        await this.InitializeAsync(token);

        // The ID inside the file must not lead to another folder than the one it was read from:
        return await this.ReadJsonAsync<ModelComparisonSeries>(this.SeriesPath(seriesId), token) is { } series
            ? series with { Id = seriesId }
            : null;
    }

    /// <summary>
    /// Stores a new comparison series.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="token">The cancellation token.</param>
    /// <exception cref="ArgumentException">The series has no ID.</exception>
    public async Task CreateSeriesAsync(ModelComparisonSeries series, CancellationToken token = default)
    {
        if (series.Id == Guid.Empty)
            throw new ArgumentException("A comparison series needs an ID.", nameof(series));

        await this.InitializeAsync(token);
        var gate = this.GetLock(series.Id);
        await gate.WaitAsync(token);
        try
        {
            await WriteJsonAtomicAsync(this.SeriesPath(series.Id), series, token);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Stores a comparison series which exists already, in place of what was stored of it before.
    /// </summary>
    /// <remarks>
    /// Guarded by the lock of the series, so that a series deleted meanwhile does not come back, e.g., with the verdict
    /// of its judge. A new series goes through CreateSeriesAsync.
    /// </remarks>
    /// <param name="series">The series.</param>
    /// <param name="token">The cancellation token.</param>
    /// <exception cref="ArgumentException">The series has no ID.</exception>
    /// <exception cref="FileNotFoundException">The series does not exist, e.g., because the user deleted it.</exception>
    public async Task SaveSeriesAsync(ModelComparisonSeries series, CancellationToken token = default)
    {
        if (series.Id == Guid.Empty)
            throw new ArgumentException("A comparison series needs an ID.", nameof(series));

        await this.InitializeAsync(token);
        var gate = this.GetLock(series.Id);
        await gate.WaitAsync(token);
        try
        {
            var seriesPath = this.SeriesPath(series.Id);
            if (!File.Exists(seriesPath))
                throw new FileNotFoundException("The comparison series does not exist.", seriesPath);

            await WriteJsonAtomicAsync(seriesPath, series, token);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Deletes a comparison series together with its chats.
    /// </summary>
    /// <param name="seriesId">The ID of the series.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task DeleteSeriesAsync(Guid seriesId, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var gate = this.GetLock(seriesId);
        await gate.WaitAsync(token);
        try
        {
            var directory = this.SeriesDirectory(seriesId);
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        finally
        {
            gate.Release();
            this.ForgetLock(seriesId);
        }
    }

    /// <summary>
    /// Loads a chat of a comparison series.
    /// </summary>
    /// <param name="seriesId">The ID of the series.</param>
    /// <param name="chatId">The ID of the chat.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The chat, or null when it has no file yet.</returns>
    /// <exception cref="InvalidDataException">This version cannot open the chat; the file stays as it is.</exception>
    public async Task<ChatThread?> LoadSeriesChatAsync(Guid seriesId, Guid chatId, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var path = this.SeriesChatPath(seriesId, chatId);
        if (!File.Exists(path))
            return null;

        ChatThread? thread;
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, true);
            thread = await JsonSerializer.DeserializeAsync<ChatThread>(stream, WorkspaceBehaviour.JSON_OPTIONS, token);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("This version of AI Studio cannot open the chat.", exception);
        }

        return thread ?? throw new InvalidDataException("This version of AI Studio cannot open the chat.");
    }

    /// <summary>
    /// Stores a chat of a comparison series, in place of what was stored of it before.
    /// </summary>
    /// <remarks>
    /// Guarded by the lock of the series, so that a series deleted meanwhile does not come back with a chat alone.
    /// </remarks>
    /// <param name="seriesId">The ID of the series.</param>
    /// <param name="thread">The chat, with its ID.</param>
    /// <param name="token">The cancellation token.</param>
    /// <exception cref="FileNotFoundException">The series does not exist, e.g., because the user deleted it while the model answered.</exception>
    public async Task SaveSeriesChatAsync(Guid seriesId, ChatThread thread, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var gate = this.GetLock(seriesId);
        await gate.WaitAsync(token);
        try
        {
            var seriesPath = this.SeriesPath(seriesId);
            if (!File.Exists(seriesPath))
                throw new FileNotFoundException("The comparison series of the chat does not exist.", seriesPath);

            await WriteJsonAtomicAsync(this.SeriesChatPath(seriesId, thread.ChatId), thread, token, WorkspaceBehaviour.JSON_OPTIONS);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Deletes a chat of a comparison series, e.g., because the user discarded its task.
    /// </summary>
    /// <param name="seriesId">The ID of the series.</param>
    /// <param name="chatId">The ID of the chat.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task DeleteSeriesChatAsync(Guid seriesId, Guid chatId, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var gate = this.GetLock(seriesId);
        await gate.WaitAsync(token);
        try
        {
            var path = this.SeriesChatPath(seriesId, chatId);
            if (File.Exists(path))
                File.Delete(path);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Reads which chats of comparison series lie on the disk, from the folders alone, e.g., for the cleanup of the
    /// provenance store at the start.
    /// </summary>
    /// <returns>The ID of the series of every chat, by the ID of the chat.</returns>
    public IReadOnlyDictionary<Guid, Guid> ReadAllSeriesChatLocations()
    {
        var locations = new Dictionary<Guid, Guid>();
        if (!Directory.Exists(this.AllSeriesDirectory))
            return locations;

        foreach (var directory in Directory.EnumerateDirectories(this.AllSeriesDirectory))
        {
            var chatsDirectory = Path.Combine(directory, CHATS_DIRECTORY_NAME);
            if (!Guid.TryParseExact(Path.GetFileName(directory), "D", out var seriesId) || !Directory.Exists(chatsDirectory))
                continue;

            foreach (var path in Directory.EnumerateFiles(chatsDirectory, "*.json"))
                if (Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "D", out var chatId))
                    locations[chatId] = seriesId;
        }

        return locations;
    }

    /// <summary>
    /// Tells when the file of a chat of a comparison series was last written.
    /// </summary>
    /// <param name="seriesId">The ID of the series.</param>
    /// <param name="chatId">The ID of the chat.</param>
    /// <returns>The time, or null when there is no file.</returns>
    public DateTimeOffset? GetSeriesChatWrittenAt(Guid seriesId, Guid chatId)
    {
        var path = this.SeriesChatPath(seriesId, chatId);
        return File.Exists(path) ? new DateTimeOffset(File.GetLastWriteTimeUtc(path)) : null;
    }
}