using System.Globalization;

using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

using Microsoft.Data.Sqlite;

namespace AIStudio.Tools.Databases.EmbeddingState;

public sealed class SqliteEmbeddingStateClientImplementation(
    string name,
    string databasePath,
    string basePath,
    string version) : EmbeddingStateClient(name, basePath)
{
    private const string DATABASE_NAME = "SQLite";
    private const string DATABASE_FILENAME = "rag-embedding-state.sqlite3";

    private readonly string databasePath = databasePath;
    private readonly string connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = databasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Shared,
        ForeignKeys = true,
        DefaultTimeout = 30,
    }.ToString();

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(SqliteEmbeddingStateClientImplementation).Namespace, nameof(SqliteEmbeddingStateClientImplementation));

    public override string CacheKey => $"{this.Name}:{this.databasePath}:{version}";

    public static async Task<DatabaseClient> CreateAsync(
        ILogger logger,
        ILogger<DatabaseClient> databaseClientLogger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(SettingsManager.DataDirectory))
            return CreateNoEmbeddingStateClient(DATABASE_NAME, "The application data directory is not available yet.", DatabaseClientStatus.STARTING, databaseClientLogger);

        try
        {
            SQLitePCL.Batteries_V2.Init();

            var basePath = Path.Combine(SettingsManager.DataDirectory, "databases", "sqlite");
            Directory.CreateDirectory(basePath);

            var databasePath = Path.Combine(basePath, DATABASE_FILENAME);
            var client = new SqliteEmbeddingStateClientImplementation(DATABASE_NAME, databasePath, basePath, string.Empty);
            await client.InitializeAsync(cancellationToken);
            var version = await client.GetSqliteVersionAsync(cancellationToken);

            client = new SqliteEmbeddingStateClientImplementation(DATABASE_NAME, databasePath, basePath, version);
            client.SetLogger(databaseClientLogger);
            return client;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "{DatabaseName} is not available. Indexed file fingerprints are disabled.", DATABASE_NAME);
            return CreateNoEmbeddingStateClient(DATABASE_NAME, exception.Message, DatabaseClientStatus.UNAVAILABLE, databaseClientLogger);
        }
    }

    public override async IAsyncEnumerable<(string Label, string Value)> GetDisplayInfo()
    {
        yield return (TB("Reported version"), version);
        yield return (TB("Database path"), this.databasePath);
        yield return (TB("Storage size"), this.GetStorageSize());
        yield return (TB("Indexed data sources"), (await this.CountAsync("data_sources", CancellationToken.None)).ToString(CultureInfo.InvariantCulture));
        yield return (TB("Indexed files"), (await this.CountAsync("embedded_files", CancellationToken.None)).ToString(CultureInfo.InvariantCulture));
    }

    public override async Task<DataSourceEmbeddingManifest> GetManifestAsync(string dataSourceId, CancellationToken token)
    {
        await using var connection = await this.OpenConnectionAsync(token);
        var manifest = new DataSourceEmbeddingManifest();

        await using (var command = CreateCommand(connection, """
                                                            SELECT embedding_provider_id, embedding_signature, source_hash, vector_size
                                                            FROM data_sources
                                                            WHERE data_source_id = $dataSourceId
                                                            """))
        {
            command.Parameters.AddWithValue("$dataSourceId", dataSourceId);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (await reader.ReadAsync(token))
            {
                manifest.EmbeddingProviderId = reader.GetString(0);
                manifest.EmbeddingSignature = reader.GetString(1);
                manifest.SourceHash = reader.GetString(2);
                manifest.VectorSize = reader.GetInt32(3);
            }
        }

        await using (var command = CreateCommand(connection, """
                                                            SELECT file_path, fingerprint, file_size, last_write_utc, embedded_at_utc, chunk_count
                                                            FROM embedded_files
                                                            WHERE data_source_id = $dataSourceId
                                                            """))
        {
            command.Parameters.AddWithValue("$dataSourceId", dataSourceId);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                manifest.Files[reader.GetString(0)] = new EmbeddedFileRecord(
                    reader.GetString(1),
                    reader.GetInt64(2),
                    ParseUtc(reader.GetString(3)),
                    ParseUtc(reader.GetString(4)),
                    reader.GetInt32(5));
            }
        }

        return manifest;
    }

    public override async Task UpsertDataSourceAsync(
        string dataSourceId,
        string dataSourceName,
        string dataSourceType,
        string embeddingProviderId,
        string embeddingSignature,
        string sourceHash,
        int vectorSize,
        CancellationToken token)
    {
        await using var connection = await this.OpenConnectionAsync(token);
        await ExecuteNonQueryAsync(connection, """
                                              INSERT INTO data_sources (
                                                  data_source_id,
                                                  data_source_name,
                                                  data_source_type,
                                                  embedding_provider_id,
                                                  embedding_signature,
                                                  source_hash,
                                                  vector_size,
                                                  updated_at_utc)
                                              VALUES (
                                                  $dataSourceId,
                                                  $dataSourceName,
                                                  $dataSourceType,
                                                  $embeddingProviderId,
                                                  $embeddingSignature,
                                                  $sourceHash,
                                                  $vectorSize,
                                                  $updatedAtUtc)
                                              ON CONFLICT(data_source_id) DO UPDATE SET
                                                  data_source_name = excluded.data_source_name,
                                                  data_source_type = excluded.data_source_type,
                                                  embedding_provider_id = excluded.embedding_provider_id,
                                                  embedding_signature = excluded.embedding_signature,
                                                  source_hash = excluded.source_hash,
                                                  vector_size = excluded.vector_size,
                                                  updated_at_utc = excluded.updated_at_utc
                                              """, token,
            ("$dataSourceId", dataSourceId),
            ("$dataSourceName", dataSourceName),
            ("$dataSourceType", dataSourceType),
            ("$embeddingProviderId", embeddingProviderId),
            ("$embeddingSignature", embeddingSignature),
            ("$sourceHash", sourceHash),
            ("$vectorSize", vectorSize),
            ("$updatedAtUtc", ToUtcText(DateTime.UtcNow)));
    }

    public override async Task UpdateVectorSizeAsync(string dataSourceId, int vectorSize, CancellationToken token)
    {
        await using var connection = await this.OpenConnectionAsync(token);
        await ExecuteNonQueryAsync(connection, """
                                              UPDATE data_sources
                                              SET vector_size = $vectorSize,
                                                  updated_at_utc = $updatedAtUtc
                                              WHERE data_source_id = $dataSourceId
                                              """, token,
            ("$dataSourceId", dataSourceId),
            ("$vectorSize", vectorSize),
            ("$updatedAtUtc", ToUtcText(DateTime.UtcNow)));
    }

    public override async Task UpdateDataSourceHashAsync(string dataSourceId, string sourceHash, CancellationToken token)
    {
        await using var connection = await this.OpenConnectionAsync(token);
        await ExecuteNonQueryAsync(connection, """
                                              UPDATE data_sources
                                              SET source_hash = $sourceHash,
                                                  updated_at_utc = $updatedAtUtc
                                              WHERE data_source_id = $dataSourceId
                                              """, token,
            ("$dataSourceId", dataSourceId),
            ("$sourceHash", sourceHash),
            ("$updatedAtUtc", ToUtcText(DateTime.UtcNow)));
    }

    public override async Task UpsertFileAsync(string dataSourceId, EmbeddingStateFile file, CancellationToken token)
    {
        await using var connection = await this.OpenConnectionAsync(token);
        await ExecuteNonQueryAsync(connection, """
                                              INSERT INTO embedded_files (
                                                  data_source_id,
                                                  file_path,
                                                  file_name,
                                                  relative_path,
                                                  fingerprint,
                                                  file_size,
                                                  last_write_utc,
                                                  embedded_at_utc,
                                                  chunk_count)
                                              VALUES (
                                                  $dataSourceId,
                                                  $filePath,
                                                  $fileName,
                                                  $relativePath,
                                                  $fingerprint,
                                                  $fileSize,
                                                  $lastWriteUtc,
                                                  $embeddedAtUtc,
                                                  $chunkCount)
                                              ON CONFLICT(data_source_id, file_path) DO UPDATE SET
                                                  file_name = excluded.file_name,
                                                  relative_path = excluded.relative_path,
                                                  fingerprint = excluded.fingerprint,
                                                  file_size = excluded.file_size,
                                                  last_write_utc = excluded.last_write_utc,
                                                  embedded_at_utc = excluded.embedded_at_utc,
                                                  chunk_count = excluded.chunk_count
                                              """, token,
            ("$dataSourceId", dataSourceId),
            ("$filePath", file.FilePath),
            ("$fileName", file.FileName),
            ("$relativePath", file.RelativePath),
            ("$fingerprint", file.Fingerprint),
            ("$fileSize", file.FileSize),
            ("$lastWriteUtc", ToUtcText(file.LastWriteUtc)),
            ("$embeddedAtUtc", ToUtcText(file.EmbeddedAtUtc)),
            ("$chunkCount", file.ChunkCount));
    }

    public override async Task DeleteFileAsync(string dataSourceId, string filePath, CancellationToken token)
    {
        await using var connection = await this.OpenConnectionAsync(token);
        await ExecuteNonQueryAsync(connection, """
                                              DELETE FROM embedded_files
                                              WHERE data_source_id = $dataSourceId
                                                AND file_path = $filePath
                                              """, token,
            ("$dataSourceId", dataSourceId),
            ("$filePath", filePath));
    }

    public override async Task DeleteDataSourceAsync(string dataSourceId, CancellationToken token)
    {
        await using var connection = await this.OpenConnectionAsync(token);
        await ExecuteNonQueryAsync(connection, """
                                              DELETE FROM data_sources
                                              WHERE data_source_id = $dataSourceId
                                              """, token,
            ("$dataSourceId", dataSourceId));
    }

    public override void Dispose()
    {
    }

    private async Task InitializeAsync(CancellationToken token)
    {
        await using var connection = await this.OpenConnectionAsync(token);
        await ExecuteNonQueryAsync(connection, """
                                              PRAGMA journal_mode=WAL;
                                              PRAGMA foreign_keys=ON;

                                              CREATE TABLE IF NOT EXISTS data_sources (
                                                  data_source_id TEXT PRIMARY KEY,
                                                  data_source_name TEXT NOT NULL,
                                                  data_source_type TEXT NOT NULL,
                                                  embedding_provider_id TEXT NOT NULL,
                                                  embedding_signature TEXT NOT NULL,
                                                  source_hash TEXT NOT NULL DEFAULT '',
                                                  vector_size INTEGER NOT NULL DEFAULT 0,
                                                  updated_at_utc TEXT NOT NULL
                                              );

                                              CREATE TABLE IF NOT EXISTS embedded_files (
                                                  data_source_id TEXT NOT NULL,
                                                  file_path TEXT COLLATE NOCASE NOT NULL,
                                                  file_name TEXT NOT NULL,
                                                  relative_path TEXT NOT NULL,
                                                  fingerprint TEXT NOT NULL,
                                                  file_size INTEGER NOT NULL,
                                                  last_write_utc TEXT NOT NULL,
                                                  embedded_at_utc TEXT NOT NULL,
                                                  chunk_count INTEGER NOT NULL,
                                                  PRIMARY KEY (data_source_id, file_path),
                                                  FOREIGN KEY (data_source_id)
                                                      REFERENCES data_sources(data_source_id)
                                                      ON DELETE CASCADE
                                              );

                                              CREATE INDEX IF NOT EXISTS idx_embedded_files_data_source
                                                  ON embedded_files(data_source_id);
                                              """, token);

        await EnsureColumnAsync(connection, "data_sources", "source_hash", "TEXT NOT NULL DEFAULT ''", token);
    }

    private async Task<string> GetSqliteVersionAsync(CancellationToken token)
    {
        await using var connection = await this.OpenConnectionAsync(token);
        await using var command = CreateCommand(connection, "SELECT sqlite_version()");
        var versionObject = await command.ExecuteScalarAsync(token);
        return Convert.ToString(versionObject, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private async Task<long> CountAsync(string tableName, CancellationToken token)
    {
        await using var connection = await this.OpenConnectionAsync(token);
        await using var command = CreateCommand(connection, $"SELECT COUNT(*) FROM {tableName}");
        var countObject = await command.ExecuteScalarAsync(token);
        return Convert.ToInt64(countObject, CultureInfo.InvariantCulture);
    }

    private static async Task EnsureColumnAsync(SqliteConnection connection, string tableName, string columnName, string columnDefinition, CancellationToken token)
    {
        await using (var command = CreateCommand(connection, $"PRAGMA table_info({tableName})"))
        {
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                    return;
            }
        }

        await ExecuteNonQueryAsync(connection, $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}", token);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken token)
    {
        var connection = new SqliteConnection(this.connectionString);
        await connection.OpenAsync(token);
        return connection;
    }

    private static SqliteCommand CreateCommand(SqliteConnection connection, string commandText)
    {
        var command = connection.CreateCommand();
        command.CommandText = commandText;
        return command;
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken token,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = CreateCommand(connection, commandText);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(token);
    }

    private static string ToUtcText(DateTime dateTime)
    {
        var utc = dateTime.Kind is DateTimeKind.Utc ? dateTime : dateTime.ToUniversalTime();
        return utc.ToString("O", CultureInfo.InvariantCulture);
    }

    private static DateTime ParseUtc(string value)
    {
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTime)
            ? dateTime.ToUniversalTime()
            : DateTime.UnixEpoch;
    }

    private static NoEmbeddingStateClient CreateNoEmbeddingStateClient(string name, string? unavailableReason, DatabaseClientStatus status, ILogger<DatabaseClient> databaseClientLogger)
    {
        var client = new NoEmbeddingStateClient(name, unavailableReason, status);
        client.SetLogger(databaseClientLogger);
        return client;
    }
}
