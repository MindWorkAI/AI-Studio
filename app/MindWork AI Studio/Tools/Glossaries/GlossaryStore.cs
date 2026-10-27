using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

using AIStudio.Settings;

namespace AIStudio.Tools.Glossaries;

/// <summary>
/// Keeps the entries of the glossaries, one file per glossary below the data directory.
/// </summary>
/// <remarks>
/// The settings hold only what describes a glossary, cf. <see cref="Glossary"/>. The path of the file
/// derives from the ID of the glossary and is never stored. The entries stay in memory once read, since
/// every translation and every lookup of the model needs them.<br/><br/>
///
/// The entries of a glossary which a configuration plugin provides live in memory only. The plugin is
/// their source and brings them again whenever the plugins load, so there is no file which would have
/// to be removed with the plugin, and a backup takes none of them along.
/// </remarks>
/// <param name="logger">The logger.</param>
/// <param name="dataDirectory">The data directory to use instead of the one of the app; meant for tests.</param>
public sealed class GlossaryStore(ILogger<GlossaryStore> logger, string? dataDirectory = null)
{
    /// <summary>
    /// The folder of the glossaries, within the data directory.
    /// </summary>
    public const string FOLDER_NAME = "glossaries";

    private const int CURRENT_FILE_VERSION = 1;

    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        WriteIndented = true,
    };

    private readonly ConcurrentDictionary<Guid, IReadOnlyList<GlossaryEntry>> cache = [];

    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> locks = [];

    private readonly ConcurrentDictionary<Guid, IReadOnlyList<GlossaryEntry>> providedEntries = [];

    private string RootDirectory => Path.Combine(
        dataDirectory ??
        SettingsManager.DataDirectory ??
        throw new InvalidOperationException("The AI Studio data directory is not initialized."),
        FOLDER_NAME);

    /// <summary>
    /// Reads the entries of a glossary for using them, e.g. for a translation.
    /// </summary>
    /// <remarks>
    /// Never use this to edit a glossary: a file which cannot be read gives no entries here, and
    /// saving them would overwrite the entries in the file. Use <see cref="this.TryLoadEntriesAsync"/> instead.
    /// </remarks>
    /// <param name="glossaryId">The ID of the glossary.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The entries; none when the glossary has no file yet or the file cannot be read.</returns>
    public async Task<IReadOnlyList<GlossaryEntry>> LoadEntriesAsync(string glossaryId, CancellationToken token = default) =>
        await this.TryLoadEntriesAsync(glossaryId, token) ?? [];

    /// <summary>
    /// Reads the entries of a glossary, telling a file which cannot be read from a glossary without entries.
    /// </summary>
    /// <param name="glossaryId">The ID of the glossary.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The entries, none when the glossary has no file yet, or null when its file cannot be read.</returns>
    public async Task<IReadOnlyList<GlossaryEntry>?> TryLoadEntriesAsync(string glossaryId, CancellationToken token = default)
    {
        var id = ParseId(glossaryId);
        if (this.providedEntries.TryGetValue(id, out var provided))
            return provided;

        if (this.cache.TryGetValue(id, out var cachedEntries))
            return cachedEntries;

        var idLock = this.locks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await idLock.WaitAsync(token);
        try
        {
            if (this.cache.TryGetValue(id, out cachedEntries))
                return cachedEntries;

            var path = this.GetFilePath(id);
            if (!File.Exists(path))
                return [];

            try
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, true);
                var file = await JsonSerializer.DeserializeAsync<GlossaryFile>(stream, JSON_OPTIONS, token);
                IReadOnlyList<GlossaryEntry> entries = file?.Entries ?? [];
                this.cache[id] = entries;
                return entries;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                //
                // Not cached on purpose: the next attempt reads the file again, e.g. once a virus
                // scanner released it. The file stays as it is, so nothing is lost before the user
                // saves the glossary again.
                //
                logger.LogError(exception, "Failed to read the entries of the glossary '{GlossaryId}' from '{Path}'.", id, path);
                return null;
            }
        }
        finally
        {
            idLock.Release();
        }
    }

    /// <summary>
    /// Replaces the entries of a glossary.
    /// </summary>
    /// <param name="glossaryId">The ID of the glossary.</param>
    /// <param name="entries">All entries of the glossary.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task SaveEntriesAsync(string glossaryId, IReadOnlyList<GlossaryEntry> entries, CancellationToken token = default)
    {
        var id = this.ParseOwnId(glossaryId);
        var entriesCopy = entries.ToList();
        var idLock = this.locks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await idLock.WaitAsync(token);
        try
        {
            var json = JsonSerializer.Serialize(new GlossaryFile(CURRENT_FILE_VERSION, entriesCopy), JSON_OPTIONS);
            await WriteTextAtomicAsync(this.GetFilePath(id), json, token);
            this.cache[id] = entriesCopy;
        }
        finally
        {
            idLock.Release();
        }
    }

    /// <summary>
    /// Removes the entries of a glossary, together with its file.
    /// </summary>
    /// <param name="glossaryId">The ID of the glossary.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task DeleteEntriesAsync(string glossaryId, CancellationToken token = default)
    {
        var id = this.ParseOwnId(glossaryId);
        var idLock = this.locks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await idLock.WaitAsync(token);
        try
        {
            this.cache.TryRemove(id, out _);

            //
            // A glossary whose entries were never saved has no file, and maybe not even the folder of
            // the glossaries, which File.Delete would answer with an exception:
            //
            var path = this.GetFilePath(id);
            if (File.Exists(path))
                File.Delete(path);
        }
        finally
        {
            idLock.Release();
        }
    }

    /// <summary>
    /// Sets the entries of a glossary which a configuration plugin provides.
    /// </summary>
    /// <param name="glossaryId">The ID of the glossary.</param>
    /// <param name="entries">All entries of the glossary.</param>
    public void SetProvidedEntries(string glossaryId, IReadOnlyList<GlossaryEntry> entries) => this.providedEntries[ParseId(glossaryId)] = entries.ToList();

    /// <summary>
    /// Forgets the provided entries of every glossary which is not among the given ones.
    /// </summary>
    /// <remarks>
    /// Called once all plugins have loaded and the glossaries of removed plugins left the settings.
    /// Forgetting them only then, rather than before the plugins load, keeps the glossaries of the
    /// remaining plugins usable while the plugins load again.
    /// </remarks>
    /// <param name="glossaryIds">The IDs of the glossaries which configuration plugins still provide.</param>
    public void RetainProvidedEntries(IEnumerable<string> glossaryIds)
    {
        var keptIds = glossaryIds.Select(id => Guid.TryParse(id, out var guid) ? guid : Guid.Empty).ToHashSet();
        foreach (var id in this.providedEntries.Keys.Where(id => !keptIds.Contains(id)))
            this.providedEntries.TryRemove(id, out _);
    }

    //
    // A glossary of a configuration plugin belongs to its plugin; nobody may change its entries here,
    // and it has no file to delete:
    //
    private Guid ParseOwnId(string glossaryId)
    {
        var id = ParseId(glossaryId);
        if (this.providedEntries.ContainsKey(id))
            throw new InvalidOperationException($"The glossary '{glossaryId}' is provided by a configuration plugin and cannot be changed.");

        return id;
    }

    //
    // Only a GUID becomes part of a path, so an ID can never point outside the folder of the glossaries:
    //
    private static Guid ParseId(string glossaryId) => Guid.TryParse(glossaryId, out var id)
        ? id
        : throw new ArgumentException($"The glossary ID '{glossaryId}' is not a valid GUID.", nameof(glossaryId));

    private string GetFilePath(Guid id) => Path.Combine(this.RootDirectory, $"{id:D}.json");

    private static async Task WriteTextAtomicAsync(string targetPath, string content, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var temporaryPath = $"{targetPath}.tmp-{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, content, new UTF8Encoding(false), token);
            File.Move(temporaryPath, targetPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    /// <summary>
    /// The content of the file of a glossary.
    /// </summary>
    /// <param name="Version">The version of the file format.</param>
    /// <param name="Entries">The entries of the glossary.</param>
    private sealed record GlossaryFile(int Version, List<GlossaryEntry> Entries);
}