using System.Data;
using System.Text.Encodings.Web;
using System.Text.Json;

using AIStudio.Settings;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.PluginSystem;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AIStudio.Tools.Databases.ProvenanceStore;

/// <summary>
/// The provenance store in SQLite, see ProvenanceStoreClient.
/// </summary>
/// <remarks>
/// Every reading and writing of entries runs on a thread of its own. SQLite works synchronously
/// behind its async methods, cf. the Databases AGENTS.md, and nearly everything this store holds is
/// a whole document, a web page, or a mail. Many callers sit in the user interface, e.g., a preview
/// which keeps its text, or a click which deletes a chat, and would freeze the window meanwhile.
/// </remarks>
public sealed class SqliteProvenanceStoreClientImplementation(string name, string databasePath, string basePath, string version) : ProvenanceStoreClient(name, basePath)
{
    private const string DATABASE_NAME = "SQLite";

    /// <summary>
    /// The file of the store, in its folder in the data folder and in a backup alike.
    /// </summary>
    public const string DATABASE_FILENAME = "provenance.sqlite3";

    /// <summary>
    /// Writes the content around a passage the way it reads: the default encoder would turn every
    /// character outside ASCII into an escape, cf. ToolExecutionResult.MODEL_CONTENT_OPTIONS.
    /// </summary>
    private static readonly JsonSerializerOptions JSON_OPTIONS = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly string databasePath = databasePath;
    private readonly DbContextOptions<ProvenanceStoreDbContext> dbContextOptions = ProvenanceStoreDbContext.CreateOptions(databasePath);

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(SqliteProvenanceStoreClientImplementation).Namespace, nameof(SqliteProvenanceStoreClientImplementation));

    public override string CacheKey => $"{this.Name}:{this.databasePath}:{version}";

    public override string Version => version;

    public static async Task<DatabaseClient> CreateAsync(
        ILogger logger,
        ILogger<DatabaseClient> databaseClientLogger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(SettingsManager.DataDirectory))
            return CreateNoProvenanceStoreClient(DATABASE_NAME, "The application data directory is not available yet.", DatabaseClientStatus.STARTING, databaseClientLogger);

        try
        {
            SQLitePCL.Batteries_V2.Init();

            var basePath = Path.Combine(SettingsManager.DataDirectory, "databases", "provenance");
            Directory.CreateDirectory(basePath);

            var databasePath = Path.Combine(basePath, DATABASE_FILENAME);
            var client = new SqliteProvenanceStoreClientImplementation(DATABASE_NAME, databasePath, basePath, string.Empty);
            await client.InitializeAsync(cancellationToken);
            var version = await client.GetSqliteVersionAsync(cancellationToken);

            client = new SqliteProvenanceStoreClientImplementation(DATABASE_NAME, databasePath, basePath, version);
            client.SetLogger(databaseClientLogger);
            return client;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "The provenance store ({DatabaseName}) is not available. Chats keep what they received in their own files.", DATABASE_NAME);
            return CreateNoProvenanceStoreClient(DATABASE_NAME, exception.Message, DatabaseClientStatus.UNAVAILABLE, databaseClientLogger);
        }
    }

    public override async IAsyncEnumerable<(string Label, string Value)> GetDisplayInfo()
    {
        //
        // Everything is read before the first line, since a yield return cannot stand in a try
        // block. Each probe catches its own failure, cf. the Databases AGENTS.md:
        //
        var schemaVersion = string.Empty;
        var journalMode = string.Empty;
        IReadOnlyDictionary<string, int>? countsByKind = null;
        int? chatCount = null;
        try
        {
            await using var context = this.CreateContext();
            schemaVersion = await GetSchemaVersionAsync(context);
            journalMode = (await QueryScalarTextAsync(context, "PRAGMA journal_mode;")).ToUpperInvariant();
            countsByKind = await TryCountByKindAsync(context);
            chatCount = await TryCountAsync(context.Entries.Select(entry => entry.ChatId).Distinct());
        }
        catch (Exception exception)
        {
            this.Logger?.LogWarning(exception, "Failed to read the display details of the provenance store.");
        }

        yield return (TB("Reported version"), version);
        yield return (TB("Journal mode"), OrUnknown(journalMode));
        yield return (TB("Schema version"), OrUnknown(schemaVersion));
        yield return (TB("Storage size"), this.GetStorageSize());

        // The total counts an entry of a kind this version does not know as well, e.g., one written by a newer version:
        yield return (TB("Stored contents in all chats"), countsByKind?.Values.Sum().CompactCount() ?? TB("unknown"));
        yield return (TB("Chats with stored contents"), chatCount?.CompactCount() ?? TB("unknown"));

        // One line per kind which any chat received, in the order of the kinds. A kind no chat received would only say zero:
        if (countsByKind is null)
            yield break;

        foreach (var kind in Enum.GetValues<ProvenanceKind>())
            if (countsByKind.TryGetValue(kind.ToString(), out var count) && count > 0)
                yield return (GetKindLabel(kind), count.CompactCount());
    }

    private static string GetKindLabel(ProvenanceKind kind) => kind switch
    {
        ProvenanceKind.ATTACHMENT => TB("Attached files"),
        ProvenanceKind.DATA_SOURCE_PASSAGE => TB("Passages of data sources"),
        ProvenanceKind.MAIL => TB("Mails"),
        ProvenanceKind.WEB_PAGE => TB("Web pages"),
        _ => kind.ToString(),
    };

    public override Task<Guid?> StoreAsync(Guid chatId, ProvenanceKind kind, string content, IReadOnlyList<string>? surroundingContent, CancellationToken token) => Task.Run<Guid?>(async () =>
    {
        var id = Guid.NewGuid();
        await using var context = this.CreateContext();
        context.Entries.Add(new ProvenanceEntity
        {
            ChatId = ToKey(chatId),
            Id = ToKey(id),
            Kind = kind.ToString(),
            Content = content,
            SurroundingContent = surroundingContent is { Count: > 0 } ? JsonSerializer.Serialize(surroundingContent, JSON_OPTIONS) : null,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });

        await context.SaveChangesAsync(token);
        return id;
    }, token);

    public override Task<IReadOnlyDictionary<Guid, ProvenanceEntry>> GetEntriesAsync(IReadOnlyCollection<Guid> ids, CancellationToken token) => Task.Run<IReadOnlyDictionary<Guid, ProvenanceEntry>>(async () =>
    {
        var entries = new Dictionary<Guid, ProvenanceEntry>();
        if (ids.Count is 0)
            return entries;

        var keys = ids.Select(ToKey).Distinct().ToList();
        await using var context = this.CreateContext();
        var rows = await context.Entries
            .AsNoTracking()
            .Where(entry => keys.Contains(entry.Id))
            .ToListAsync(token);

        // Copies of an entry hold the same content, so the first one found stands for all of them:
        foreach (var row in rows)
        {
            if (!Guid.TryParse(row.Id, out var id) || entries.ContainsKey(id))
                continue;

            if (!Enum.TryParse<ProvenanceKind>(row.Kind, out var kind))
            {
                this.Logger?.LogWarning("The provenance entry '{EntryId}' has the unknown kind '{Kind}', e.g., from a newer version of AI Studio, and is left out.", row.Id, row.Kind);
                continue;
            }

            entries[id] = new ProvenanceEntry(id, kind, row.Content, this.ReadSurroundingContent(row), row.CreatedAtUtc);
        }

        return entries;
    }, token);

    public override Task CopyChatAsync(Guid sourceChatId, Guid targetChatId, CancellationToken token) => Task.Run(async () =>
    {
        var sourceKey = ToKey(sourceChatId);
        var targetKey = ToKey(targetChatId);
        await using var context = this.CreateContext();

        //
        // In one statement, so no row passes through this process. The time of each row stays,
        // since it says when the AI received the content, not when the chat was copied:
        //
        await context.Database.ExecuteSqlAsync(
            $"""
             INSERT OR IGNORE INTO provenance (chat_id, id, kind, content, surrounding_content, created_at_utc)
             SELECT {targetKey}, id, kind, content, surrounding_content, created_at_utc
             FROM provenance
             WHERE chat_id = {sourceKey}
             """,
            token);
    }, token);

    public override Task<int> DeleteChatsAsync(IReadOnlyCollection<Guid> chatIds, CancellationToken token, DateTimeOffset? createdBefore = null) => Task.Run(async () =>
    {
        if (chatIds.Count is 0)
            return 0;

        var chatKeys = chatIds.Select(ToKey).Distinct().ToList();
        await using var context = this.CreateContext();
        return await CreatedBefore(context.Entries.Where(entry => chatKeys.Contains(entry.ChatId)), createdBefore).ExecuteDeleteAsync(token);
    }, token);

    public override Task<int> DeleteChatEntriesExceptAsync(Guid chatId, IReadOnlyCollection<Guid> keptIds, CancellationToken token, DateTimeOffset? createdBefore = null) => Task.Run(async () =>
    {
        var chatKey = ToKey(chatId);
        var keptKeys = keptIds.Select(ToKey).Distinct().ToList();
        await using var context = this.CreateContext();
        return await CreatedBefore(context.Entries.Where(entry => entry.ChatId == chatKey && !keptKeys.Contains(entry.Id)), createdBefore).ExecuteDeleteAsync(token);
    }, token);

    public override Task<int> DeleteEntriesAsync(Guid chatId, IReadOnlyCollection<Guid> ids, CancellationToken token) => Task.Run(async () =>
    {
        if (ids.Count is 0)
            return 0;

        var chatKey = ToKey(chatId);
        var keys = ids.Select(ToKey).Distinct().ToList();
        await using var context = this.CreateContext();
        return await context.Entries.Where(entry => entry.ChatId == chatKey && keys.Contains(entry.Id)).ExecuteDeleteAsync(token);
    }, token);

    /// <summary>
    /// Narrows entries down to those stored before a moment.
    /// </summary>
    /// <remarks>
    /// The time is stored as text in the round-trip format of UTC, whose order is the order of time,
    /// so SQLite compares it as text, after the parameter went through the same conversion.
    /// </remarks>
    private static IQueryable<ProvenanceEntity> CreatedBefore(IQueryable<ProvenanceEntity> entries, DateTimeOffset? createdBefore) => createdBefore is { } moment
        ? entries.Where(entry => entry.CreatedAtUtc < moment)
        : entries;

    public override Task<IReadOnlyList<Guid>> GetChatIdsAsync(CancellationToken token) => Task.Run<IReadOnlyList<Guid>>(async () =>
    {
        await using var context = this.CreateContext();
        var chatKeys = await context.Entries.Select(entry => entry.ChatId).Distinct().ToListAsync(token);

        // Guid.Empty included: entries stored for a chat without an id belong to no chat on the disk, so the cleanup takes them:
        return chatKeys
            .Select(chatKey => Guid.TryParse(chatKey, out var chatId) ? chatId : (Guid?)null)
            .OfType<Guid>()
            .ToList();
    }, token);

    /// <inheritdoc />
    /// <remarks>
    /// A consistent snapshot which holds back no writer, made on a thread of its own, cf. SqliteDatabaseCopy.
    /// </remarks>
    public override Task<string> BackupToDirectoryAsync(string targetDirectory, CancellationToken token) => Task.Run(async () =>
    {
        var targetPath = Path.Join(targetDirectory, DATABASE_FILENAME);
        await SqliteDatabaseCopy.CopyAndCheckAsync(this.databasePath, targetPath, token);
        return targetPath;
    }, token);

    /// <inheritdoc />
    /// <remarks>
    /// The copy is read on a connection of its own, read-only and without a pool, so nothing keeps it
    /// open afterward and the caller can delete it. Only the columns this version knows are read: a
    /// copy made by a newer version may hold more.
    /// </remarks>
    public override async Task<int> ImportChatAsync(string sourceDatabasePath, Guid sourceChatId, Guid targetChatId, CancellationToken token)
    {
        var targetKey = ToKey(targetChatId);
        var rows = new List<ProvenanceEntity>();
        await using (var connection = new SqliteConnection(SqliteDatabaseCopy.BuildConnectionString(sourceDatabasePath, SqliteOpenMode.ReadOnly)))
        {
            await connection.OpenAsync(token);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT id, kind, content, surrounding_content, created_at_utc FROM provenance WHERE chat_id = $chatId;";
            command.Parameters.AddWithValue("$chatId", ToKey(sourceChatId));

            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                rows.Add(new ProvenanceEntity
                {
                    ChatId = targetKey,
                    Id = reader.GetString(0),
                    Kind = reader.GetString(1),
                    Content = reader.GetString(2),
                    SurroundingContent = reader.IsDBNull(3) ? null : reader.GetString(3),
                    CreatedAtUtc = IndexStoreDateTimeOffset.ParseUtc(reader.GetString(4)),
                });
            }
        }

        if (rows.Count is 0)
            return 0;

        await using var context = this.CreateContext();
        var knownIds = (await context.Entries.Where(entry => entry.ChatId == targetKey).Select(entry => entry.Id).ToListAsync(token)).ToHashSet(StringComparer.Ordinal);
        var newRows = rows.Where(row => knownIds.Add(row.Id)).ToList();
        context.Entries.AddRange(newRows);
        await context.SaveChangesAsync(token);
        return newRows.Count;
    }

    public override void Dispose()
    {
    }

    /// <summary>
    /// Writes an id the way this store keeps it: as text, lowercase and with hyphens.
    /// </summary>
    private static string ToKey(Guid id) => id.ToString("D");

    private IReadOnlyList<string> ReadSurroundingContent(ProvenanceEntity row)
    {
        if (string.IsNullOrWhiteSpace(row.SurroundingContent))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(row.SurroundingContent) ?? [];
        }
        catch (JsonException exception)
        {
            this.Logger?.LogWarning(exception, "The content around the provenance entry '{EntryId}' cannot be read and is left out.", row.Id);
            return [];
        }
    }

    private static string OrUnknown(string value) => string.IsNullOrWhiteSpace(value) ? TB("unknown") : value;

    private static async Task<string> QueryScalarTextAsync(ProvenanceStoreDbContext context, string sql)
    {
        try
        {
            // The raw connection rather than SqlQueryRaw, which cannot run a PRAGMA, cf. the Databases AGENTS.md:
            var connection = context.Database.GetDbConnection();
            if (connection.State is not ConnectionState.Open)
                await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            var result = await command.ExecuteScalarAsync();
            return result?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task<string> GetSchemaVersionAsync(ProvenanceStoreDbContext context)
    {
        try
        {
            var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync()).ToList();
            if (appliedMigrations.Count == 0)
                return TB("no migration applied");

            var pendingMigrations = (await context.Database.GetPendingMigrationsAsync()).ToList();
            return pendingMigrations.Count == 0
                ? string.Format(I18N.I.Culture, TB("{0} ({1} applied)"), appliedMigrations[^1], appliedMigrations.Count.CompactCount())
                : string.Format(I18N.I.Culture, TB("{0} ({1} applied, {2} pending)"), appliedMigrations[^1], appliedMigrations.Count.CompactCount(), pendingMigrations.Count.CompactCount());
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Counts the entries of every kind, by the name each kind is stored with.
    /// </summary>
    /// <returns>The counts by kind, or null when the count failed.</returns>
    private static async Task<IReadOnlyDictionary<string, int>?> TryCountByKindAsync(ProvenanceStoreDbContext context)
    {
        try
        {
            return await context.Entries
                .GroupBy(entry => entry.Kind)
                .Select(group => new { Kind = group.Key, Count = group.Count() })
                .ToDictionaryAsync(group => group.Kind, group => group.Count);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<int?> TryCountAsync<T>(IQueryable<T> query)
    {
        try
        {
            return await query.CountAsync();
        }
        catch
        {
            return null;
        }
    }

    private async Task InitializeAsync(CancellationToken token)
    {
        await using var context = this.CreateContext();
        await ProvenanceStoreSchemaMigrator.MigrateAsync(context, token);
    }

    private async Task<string> GetSqliteVersionAsync(CancellationToken token)
    {
        await using var context = this.CreateContext();
        var versions = await context.Database
            .SqlQueryRaw<string>("SELECT sqlite_version() AS Value")
            .ToListAsync(token);
        return versions.FirstOrDefault() ?? string.Empty;
    }

    private ProvenanceStoreDbContext CreateContext() => new(this.dbContextOptions);

    private static NoProvenanceStoreClient CreateNoProvenanceStoreClient(string name, string? unavailableReason, DatabaseClientStatus status, ILogger<DatabaseClient> databaseClientLogger)
    {
        var client = new NoProvenanceStoreClient(name, unavailableReason, status);
        client.SetLogger(databaseClientLogger);
        return client;
    }
}