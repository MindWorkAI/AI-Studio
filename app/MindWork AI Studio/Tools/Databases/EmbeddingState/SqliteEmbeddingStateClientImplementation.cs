using System.Globalization;
using System.Text.RegularExpressions;

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
    private const string DATABASE_NAME = "Local RAG Index";
    private const string DATABASE_FILENAME = "rag-index.sqlite3";
    private const int MAX_FTS_QUERY_TERMS = 32;

    private static readonly Regex FTS_TOKEN_REGEX = new(@"[\p{L}\p{Nd}_]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
            logger.LogWarning(exception, "{DatabaseName} is not available. Indexed file fingerprints and search chunks are disabled.", DATABASE_NAME);
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
        yield return (TB("Search chunks"), (await this.CountAsync("embedding_chunks", CancellationToken.None)).ToString(CultureInfo.InvariantCulture));
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
                                                            SELECT absolute_path, fingerprint, file_size, last_write_utc, embedded_at_utc, chunk_count
                                                            FROM embedded_files
                                                            WHERE data_source_id = $dataSourceId
                                                              AND chunk_count > 0
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
                                                  parent_file_id,
                                                  data_source_id,
                                                  absolute_path,
                                                  file_name,
                                                  relative_path,
                                                  file_type,
                                                  fingerprint,
                                                  file_size,
                                                  creation_utc,
                                                  last_write_utc,
                                                  embedded_at_utc,
                                                  chunk_count,
                                                  compliance_level,
                                                  compliance_level_rank)
                                              VALUES (
                                                  $parentFileId,
                                                  $dataSourceId,
                                                  $absolutePath,
                                                  $fileName,
                                                  $relativePath,
                                                  $fileType,
                                                  $fingerprint,
                                                  $fileSize,
                                                  $creationUtc,
                                                  $lastWriteUtc,
                                                  $embeddedAtUtc,
                                                  $chunkCount,
                                                  $complianceLevel,
                                                  $complianceLevelRank)
                                              ON CONFLICT(parent_file_id) DO UPDATE SET
                                                  data_source_id = excluded.data_source_id,
                                                  absolute_path = excluded.absolute_path,
                                                  file_name = excluded.file_name,
                                                  relative_path = excluded.relative_path,
                                                  file_type = excluded.file_type,
                                                  fingerprint = excluded.fingerprint,
                                                  file_size = excluded.file_size,
                                                  creation_utc = excluded.creation_utc,
                                                  last_write_utc = excluded.last_write_utc,
                                                  embedded_at_utc = excluded.embedded_at_utc,
                                                  chunk_count = excluded.chunk_count,
                                                  compliance_level = excluded.compliance_level,
                                                  compliance_level_rank = excluded.compliance_level_rank
                                              """, token,
            ("$parentFileId", file.ParentFileId),
            ("$dataSourceId", dataSourceId),
            ("$absolutePath", file.AbsolutePath),
            ("$fileName", file.FileName),
            ("$relativePath", file.RelativePath),
            ("$fileType", file.FileType),
            ("$fingerprint", file.Fingerprint),
            ("$fileSize", file.FileSize),
            ("$creationUtc", ToUtcText(file.CreationUtc)),
            ("$lastWriteUtc", ToUtcText(file.LastWriteUtc)),
            ("$embeddedAtUtc", ToUtcText(file.EmbeddedAtUtc)),
            ("$chunkCount", file.ChunkCount),
            ("$complianceLevel", file.ComplianceLevel),
            ("$complianceLevelRank", file.ComplianceLevelRank));
    }

    public override async Task DeleteFileAsync(string dataSourceId, string filePath, CancellationToken token)
    {
        await using var connection = await this.OpenConnectionAsync(token);
        await ExecuteNonQueryAsync(connection, """
                                              DELETE FROM embedded_files
                                              WHERE data_source_id = $dataSourceId
                                                AND absolute_path = $filePath
                                              """, token,
            ("$dataSourceId", dataSourceId),
            ("$filePath", filePath));
    }

    public override async Task UpsertChunksAsync(string dataSourceId, IReadOnlyList<EmbeddingStateChunk> chunks, CancellationToken token)
    {
        if (chunks.Count == 0)
            return;

        await using var connection = await this.OpenConnectionAsync(token);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(token);

        foreach (var chunk in chunks)
        {
            token.ThrowIfCancellationRequested();

            await ExecuteNonQueryAsync(connection, transaction, """
                                                               INSERT INTO embedding_chunks (
                                                                   chunk_id,
                                                                   parent_file_id,
                                                                   page_number,
                                                                   chunk_index,
                                                                   chunk_text,
                                                                   embedded_at_utc)
                                                               VALUES (
                                                                   $chunkId,
                                                                   $parentFileId,
                                                                   $pageNumber,
                                                                   $chunkIndex,
                                                                   $chunkText,
                                                                   $embeddedAtUtc)
                                                               ON CONFLICT(chunk_id) DO UPDATE SET
                                                                   parent_file_id = excluded.parent_file_id,
                                                                   page_number = excluded.page_number,
                                                                   chunk_index = excluded.chunk_index,
                                                                   chunk_text = excluded.chunk_text,
                                                                   embedded_at_utc = excluded.embedded_at_utc
                                                               """, token,
                ("$chunkId", chunk.ChunkId),
                ("$parentFileId", chunk.ParentFileId),
                ("$pageNumber", chunk.PageNumber),
                ("$chunkIndex", chunk.ChunkIndex),
                ("$chunkText", chunk.ChunkText),
                ("$embeddedAtUtc", ToUtcText(chunk.EmbeddedAtUtc)));
        }

        await transaction.CommitAsync(token);
    }

    public override async Task<IReadOnlyList<EmbeddingStateSearchResult>> SearchChunksAsync(string dataSourceId, string query, int maxMatches, CancellationToken token)
    {
        if (maxMatches <= 0)
            return [];

        var ftsQuery = BuildFtsQuery(query);
        if (string.IsNullOrWhiteSpace(ftsQuery))
            return [];

        var results = new List<EmbeddingStateSearchResult>(maxMatches);
        await using var connection = await this.OpenConnectionAsync(token);
        await using var command = CreateCommand(connection, """
                                                           SELECT
                                                               c.chunk_id,
                                                               c.parent_file_id,
                                                               ds.data_source_id,
                                                               ds.data_source_name,
                                                               ds.data_source_type,
                                                               f.absolute_path,
                                                               f.file_name,
                                                               f.relative_path,
                                                               f.file_type,
                                                               c.page_number,
                                                               c.chunk_index,
                                                               c.chunk_text,
                                                               bm25(embedding_chunks_fts) AS score,
                                                               f.fingerprint,
                                                               f.file_size,
                                                               f.creation_utc,
                                                               f.last_write_utc,
                                                               c.embedded_at_utc,
                                                               f.chunk_count,
                                                               f.compliance_level,
                                                               f.compliance_level_rank
                                                           FROM embedding_chunks_fts
                                                           JOIN embedding_chunks c ON c.id = embedding_chunks_fts.rowid
                                                           JOIN embedded_files f ON f.parent_file_id = c.parent_file_id
                                                           JOIN data_sources ds ON ds.data_source_id = f.data_source_id
                                                           WHERE ds.data_source_id = $dataSourceId
                                                             AND embedding_chunks_fts MATCH $query
                                                           ORDER BY score
                                                           LIMIT $maxMatches
                                                           """);

        command.Parameters.AddWithValue("$dataSourceId", dataSourceId);
        command.Parameters.AddWithValue("$query", ftsQuery);
        command.Parameters.AddWithValue("$maxMatches", maxMatches);

        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            results.Add(new EmbeddingStateSearchResult(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetInt32(9),
                reader.GetInt32(10),
                reader.GetString(11),
                reader.GetDouble(12),
                reader.GetString(13),
                reader.GetInt64(14),
                ParseUtc(reader.GetString(15)),
                ParseUtc(reader.GetString(16)),
                ParseUtc(reader.GetString(17)),
                reader.GetInt32(18),
                reader.GetString(19),
                reader.GetInt32(20)));
        }

        return results;
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
                                                  parent_file_id TEXT PRIMARY KEY,
                                                  data_source_id TEXT NOT NULL,
                                                  absolute_path TEXT COLLATE NOCASE NOT NULL,
                                                  file_name TEXT NOT NULL,
                                                  relative_path TEXT NOT NULL,
                                                  file_type TEXT NOT NULL,
                                                  fingerprint TEXT NOT NULL,
                                                  file_size INTEGER NOT NULL,
                                                  creation_utc TEXT NOT NULL,
                                                  last_write_utc TEXT NOT NULL,
                                                  embedded_at_utc TEXT NOT NULL,
                                                  chunk_count INTEGER NOT NULL,
                                                  compliance_level TEXT NOT NULL,
                                                  compliance_level_rank INTEGER NOT NULL,
                                                  FOREIGN KEY (data_source_id)
                                                      REFERENCES data_sources(data_source_id)
                                                      ON DELETE CASCADE,
                                                  UNIQUE(data_source_id, absolute_path)
                                              );

                                              CREATE TABLE IF NOT EXISTS embedding_chunks (
                                                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                                                  chunk_id TEXT NOT NULL UNIQUE,
                                                  parent_file_id TEXT NOT NULL,
                                                  page_number INTEGER NULL,
                                                  chunk_index INTEGER NOT NULL,
                                                  chunk_text TEXT NOT NULL,
                                                  embedded_at_utc TEXT NOT NULL,
                                                  FOREIGN KEY (parent_file_id)
                                                      REFERENCES embedded_files(parent_file_id)
                                                      ON DELETE CASCADE,
                                                  UNIQUE(parent_file_id, chunk_index)
                                              );

                                              CREATE INDEX IF NOT EXISTS idx_embedded_files_data_source
                                                  ON embedded_files(data_source_id);

                                              CREATE INDEX IF NOT EXISTS idx_embedded_files_absolute_path
                                                  ON embedded_files(absolute_path);

                                              CREATE INDEX IF NOT EXISTS idx_embedded_files_file_type
                                                  ON embedded_files(file_type);

                                              CREATE INDEX IF NOT EXISTS idx_embedded_files_compliance
                                                  ON embedded_files(compliance_level_rank);

                                              CREATE INDEX IF NOT EXISTS idx_embedding_chunks_parent_file
                                                  ON embedding_chunks(parent_file_id);

                                              CREATE INDEX IF NOT EXISTS idx_embedding_chunks_page
                                                  ON embedding_chunks(page_number);

                                              CREATE VIRTUAL TABLE IF NOT EXISTS embedding_chunks_fts
                                                  USING fts5(chunk_id UNINDEXED, file_name, chunk_text);

                                              CREATE TRIGGER IF NOT EXISTS embedding_chunks_ai
                                                  AFTER INSERT ON embedding_chunks
                                              BEGIN
                                                  INSERT INTO embedding_chunks_fts(rowid, chunk_id, file_name, chunk_text)
                                                  VALUES (
                                                      new.id,
                                                      new.chunk_id,
                                                      (SELECT file_name FROM embedded_files WHERE parent_file_id = new.parent_file_id),
                                                      new.chunk_text);
                                              END;

                                              CREATE TRIGGER IF NOT EXISTS embedding_chunks_ad
                                                  AFTER DELETE ON embedding_chunks
                                              BEGIN
                                                  DELETE FROM embedding_chunks_fts
                                                  WHERE rowid = old.id;
                                              END;

                                              CREATE TRIGGER IF NOT EXISTS embedding_chunks_au
                                                  AFTER UPDATE ON embedding_chunks
                                              BEGIN
                                                  DELETE FROM embedding_chunks_fts
                                                  WHERE rowid = old.id;

                                                  INSERT INTO embedding_chunks_fts(rowid, chunk_id, file_name, chunk_text)
                                                  VALUES (
                                                      new.id,
                                                      new.chunk_id,
                                                      (SELECT file_name FROM embedded_files WHERE parent_file_id = new.parent_file_id),
                                                      new.chunk_text);
                                              END;

                                              CREATE TRIGGER IF NOT EXISTS embedded_files_file_name_au
                                                  AFTER UPDATE OF file_name ON embedded_files
                                              BEGIN
                                                  DELETE FROM embedding_chunks_fts
                                                  WHERE rowid IN (
                                                      SELECT id
                                                      FROM embedding_chunks
                                                      WHERE parent_file_id = new.parent_file_id
                                                  );

                                                  INSERT INTO embedding_chunks_fts(rowid, chunk_id, file_name, chunk_text)
                                                  SELECT id, chunk_id, new.file_name, chunk_text
                                                  FROM embedding_chunks
                                                  WHERE parent_file_id = new.parent_file_id;
                                              END;
                                              """, token);
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

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string commandText,
        CancellationToken token,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = CreateCommand(connection, commandText);
        command.Transaction = transaction;
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

    private static string BuildFtsQuery(string query)
    {
        var terms = FTS_TOKEN_REGEX
            .Matches(query)
            .Select(match => match.Value)
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MAX_FTS_QUERY_TERMS)
            .Select(term => $"\"{term.Replace("\"", "\"\"", StringComparison.Ordinal)}\"")
            .ToList();

        return terms.Count == 0 ? string.Empty : string.Join(" OR ", terms);
    }

    private static NoEmbeddingStateClient CreateNoEmbeddingStateClient(string name, string? unavailableReason, DatabaseClientStatus status, ILogger<DatabaseClient> databaseClientLogger)
    {
        var client = new NoEmbeddingStateClient(name, unavailableReason, status);
        client.SetLogger(databaseClientLogger);
        return client;
    }
}
