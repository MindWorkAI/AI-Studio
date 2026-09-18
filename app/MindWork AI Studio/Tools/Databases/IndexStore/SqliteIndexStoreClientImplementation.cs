using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;

using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

using Microsoft.EntityFrameworkCore;

namespace AIStudio.Tools.Databases.IndexStore;

public sealed class SqliteIndexStoreClientImplementation(string name, string databasePath, string basePath, string version) : IndexStoreClient(name, basePath)
{
    private const string DATABASE_NAME = "SQLite";
    private const string DATABASE_FILENAME = "rag-index.sqlite3";
    private const int MAX_FTS_QUERY_TERMS = 32;
    private const int CHUNK_UPSERT_BATCH_SIZE = 500;

    private static readonly Regex FTS_TOKEN_REGEX = new(@"[\p{L}\p{Nd}_]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string databasePath = databasePath;
    private readonly DbContextOptions<IndexStoreDbContext> dbContextOptions = IndexStoreDbContext.CreateOptions(databasePath);

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(SqliteIndexStoreClientImplementation).Namespace, nameof(SqliteIndexStoreClientImplementation));

    public override string CacheKey => $"{this.Name}:{this.databasePath}:{version}";

    public override string Version => version;

    public static async Task<DatabaseClient> CreateAsync(
        ILogger logger,
        ILogger<DatabaseClient> databaseClientLogger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(SettingsManager.DataDirectory))
            return CreateNoIndexStoreClient(DATABASE_NAME, "The application data directory is not available yet.", DatabaseClientStatus.STARTING, databaseClientLogger);

        try
        {
            SQLitePCL.Batteries_V2.Init();

            var basePath = Path.Combine(SettingsManager.DataDirectory, "databases", "sqlite");
            Directory.CreateDirectory(basePath);

            var databasePath = Path.Combine(basePath, DATABASE_FILENAME);
            var client = new SqliteIndexStoreClientImplementation(DATABASE_NAME, databasePath, basePath, string.Empty);
            await client.InitializeAsync(cancellationToken);
            var version = await client.GetSqliteVersionAsync(cancellationToken);

            client = new SqliteIndexStoreClientImplementation(DATABASE_NAME, databasePath, basePath, version);
            client.SetLogger(databaseClientLogger);
            return client;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "{DatabaseName} is not available. Indexed file fingerprints and search chunks are disabled.", DATABASE_NAME);
            return CreateNoIndexStoreClient(DATABASE_NAME, exception.Message, DatabaseClientStatus.UNAVAILABLE, databaseClientLogger);
        }
    }

    public override async IAsyncEnumerable<(string Label, string Value)> GetDisplayInfo()
    {
        //
        // Read everything before yielding the first line: this is an iterator, and a try/catch
        // cannot wrap a yield return. Each probe therefore catches its own failure and answers
        // with an empty string, which shows up as "unknown" below. Without that, a single failing
        // PRAGMA would throw out of here and the information page would replace the entire block
        // with the fallback client.
        //
        var snapshot = await this.ReadDisplaySnapshotAsync();

        yield return (TB("Reported version"), version);
        yield return (TB("Native library"), OrUnknown(SqliteRuntimeInfo.GetNativeLibraryName()));
        yield return (TB("Wrapper version"), OrUnknown(SqliteRuntimeInfo.GetWrapperVersion()));
        yield return (TB("Process architecture"), OrUnknown(SqliteRuntimeInfo.GetProcessArchitecture()));

        // Only worth a line when the process runs on a foreign architecture, Rosetta above all:
        var systemArchitecture = SqliteRuntimeInfo.GetSystemArchitecture();
        if (!string.IsNullOrWhiteSpace(systemArchitecture))
            yield return (TB("System architecture"), systemArchitecture);

        yield return (TB("Full-text search (FTS5)"), OrUnknown(snapshot.FullTextSearch));
        yield return (TB("Journal mode"), OrUnknown(snapshot.JournalMode));
        yield return (TB("Schema version"), OrUnknown(snapshot.SchemaVersion));
        yield return (TB("Database tables"), OrUnknown(snapshot.TableCount));
        yield return (TB("Storage size"), this.GetStorageSize());
        yield return (TB("Indexed data sources"), OrUnknown(snapshot.DataSourceCount));
        yield return (TB("Indexed files"), OrUnknown(snapshot.FileCount));
        yield return (TB("Permanently skipped files"), OrUnknown(snapshot.FailureCount));
    }

    public override async Task<DataSourceIndexState?> GetDataSourceStateAsync(string dataSourceId, CancellationToken token)
    {
        await using var context = this.CreateContext();
        return await context.DataSources
            .AsNoTracking()
            .Where(source => source.DataSourceId == dataSourceId)
            .Select(source => new DataSourceIndexState(source.EmbeddingProviderId, source.EmbeddingSignature, source.SourceHash, source.VectorSize))
            .FirstOrDefaultAsync(token);
    }

    public override async Task<DataSourceEmbeddingManifest> GetManifestAsync(string dataSourceId, CancellationToken token)
    {
        await using var context = this.CreateContext();
        var manifest = new DataSourceEmbeddingManifest();

        var dataSource = await context.DataSources
            .AsNoTracking()
            .FirstOrDefaultAsync(source => source.DataSourceId == dataSourceId, token);

        if (dataSource is null)
            return manifest;

        manifest.EmbeddingProviderId = dataSource.EmbeddingProviderId;
        manifest.EmbeddingSignature = dataSource.EmbeddingSignature;
        manifest.SourceHash = dataSource.SourceHash;
        manifest.VectorSize = dataSource.VectorSize;

        var files = await context.EmbeddedFiles
            .AsNoTracking()
            .Where(file => file.DataSourceId == dataSourceId && file.ChunkCount > 0)
            .ToListAsync(token);
        foreach (var file in files)
        {
            manifest.Files[file.AbsolutePath] = new EmbeddedFileRecord(
                file.Fingerprint,
                file.FileSize,
                file.LastWriteUtc,
                file.EmbeddedAtUtc,
                file.ChunkCount);
        }

        var permanentFailures = await context.PermanentIndexingFailures
            .AsNoTracking()
            .Where(failure => failure.DataSourceId == dataSourceId)
            .ToListAsync(token);
        foreach (var failure in permanentFailures)
        {
            manifest.PermanentFailures[failure.AbsolutePath] = new PermanentIndexingFailureRecord(
                failure.Fingerprint,
                ParseFailureCode(failure.FailureCode),
                failure.FailureMessage,
                failure.OccurredAtUtc);
        }

        return manifest;
    }

    public override async Task UpsertDataSourceAsync(
        string dataSourceId,
        string dataSourceType,
        string embeddingProviderId,
        string embeddingSignature,
        string sourceHash,
        int vectorSize,
        CancellationToken token)
    {
        await using var context = this.CreateContext();
        var dataSource = await context.DataSources.FirstOrDefaultAsync(source => source.DataSourceId == dataSourceId, token);
        if (dataSource is null)
        {
            dataSource = new EmbeddingStateDataSourceEntity
            {
                DataSourceId = dataSourceId,
            };
            context.DataSources.Add(dataSource);
        }

        ApplyDataSource(dataSource, dataSourceType, embeddingProviderId, embeddingSignature, sourceHash, vectorSize);
        await context.SaveChangesAsync(token);
    }

    public override async Task UpdateVectorSizeAsync(string dataSourceId, int vectorSize, CancellationToken token)
    {
        await using var context = this.CreateContext();
        var dataSource = await context.DataSources.FirstOrDefaultAsync(source => source.DataSourceId == dataSourceId, token);
        if (dataSource is null)
            return;

        dataSource.VectorSize = vectorSize;
        dataSource.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(token);
    }

    public override async Task UpdateDataSourceHashAsync(string dataSourceId, string sourceHash, CancellationToken token)
    {
        await using var context = this.CreateContext();
        var dataSource = await context.DataSources.FirstOrDefaultAsync(source => source.DataSourceId == dataSourceId, token);
        if (dataSource is null)
            return;

        dataSource.SourceHash = sourceHash;
        dataSource.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(token);
    }

    public override async Task UpsertFileAsync(string dataSourceId, EmbeddingStateFile file, CancellationToken token)
    {
        await using var context = this.CreateContext();
        var fileEntity = await context.EmbeddedFiles.FirstOrDefaultAsync(entity => entity.ParentFileId == file.ParentFileId, token);
        if (fileEntity is null)
        {
            fileEntity = new EmbeddingStateFileEntity
            {
                ParentFileId = file.ParentFileId,
            };
            context.EmbeddedFiles.Add(fileEntity);
        }

        ApplyFile(fileEntity, dataSourceId, file);
        await context.SaveChangesAsync(token);
    }

    public override async Task DeleteFileAsync(string dataSourceId, string filePath, CancellationToken token)
    {
        await using var context = this.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(token);

        var parentFileIds = await context.EmbeddedFiles
            .Where(file => file.DataSourceId == dataSourceId && file.AbsolutePath == filePath)
            .Select(file => file.ParentFileId)
            .ToListAsync(token);

        foreach (var parentFileIdBatch in parentFileIds.Chunk(CHUNK_UPSERT_BATCH_SIZE))
            await context.EmbeddingChunks
                .Where(chunk => parentFileIdBatch.Contains(chunk.ParentFileId))
                .ExecuteDeleteAsync(token);

        await context.EmbeddedFiles
            .Where(file => file.DataSourceId == dataSourceId && file.AbsolutePath == filePath)
            .ExecuteDeleteAsync(token);

        await transaction.CommitAsync(token);
    }

    public override async Task UpsertPermanentFailureAsync(string dataSourceId, PermanentIndexingFailure failure, CancellationToken token)
    {
        await using var context = this.CreateContext();
        var failureEntity = await context.PermanentIndexingFailures.FirstOrDefaultAsync(entity => entity.ParentFileId == failure.ParentFileId, token);
        if (failureEntity is null)
        {
            failureEntity = new IndexingFailureEntity
            {
                ParentFileId = failure.ParentFileId,
            };
            context.PermanentIndexingFailures.Add(failureEntity);
        }

        ApplyPermanentFailure(failureEntity, dataSourceId, failure);
        await context.SaveChangesAsync(token);
    }

    public override async Task DeletePermanentFailureAsync(string dataSourceId, string filePath, CancellationToken token)
    {
        await using var context = this.CreateContext();
        await context.PermanentIndexingFailures
            .Where(failure => failure.DataSourceId == dataSourceId && failure.AbsolutePath == filePath)
            .ExecuteDeleteAsync(token);
    }

    public override async Task UpsertChunksAsync(string dataSourceId, IReadOnlyList<EmbeddingStateChunk> chunks, CancellationToken token)
    {
        if (chunks.Count == 0)
            return;

        await using var context = this.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(token);

        foreach (var chunkBatch in chunks.Chunk(CHUNK_UPSERT_BATCH_SIZE))
        {
            token.ThrowIfCancellationRequested();

            var chunkIds = chunkBatch
                .Select(chunk => chunk.ChunkId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var existingChunks = await context.EmbeddingChunks
                .Where(chunk => chunkIds.Contains(chunk.ChunkId))
                .ToDictionaryAsync(chunk => chunk.ChunkId, StringComparer.Ordinal, token);

            foreach (var chunk in chunkBatch)
            {
                if (!existingChunks.TryGetValue(chunk.ChunkId, out var chunkEntity))
                {
                    chunkEntity = new EmbeddingStateChunkEntity
                    {
                        ChunkId = chunk.ChunkId,
                    };
                    context.EmbeddingChunks.Add(chunkEntity);
                    existingChunks[chunk.ChunkId] = chunkEntity;
                }

                ApplyChunk(chunkEntity, chunk);
            }

            await context.SaveChangesAsync(token);
            context.ChangeTracker.Clear();
        }

        await transaction.CommitAsync(token);
    }

    public override async Task<IReadOnlyList<IndexStoreSearchResult>> SearchChunksAsync(string dataSourceId, string query, int maxMatches, CancellationToken token)
    {
        if (maxMatches <= 0)
            return [];

        var ftsQuery = BuildFtsQuery(query);
        if (string.IsNullOrWhiteSpace(ftsQuery))
            return [];

        await using var context = this.CreateContext();
        var results = await context.SearchResults
            .FromSqlInterpolated($"""
                                  SELECT
                                      c.chunk_id AS ChunkId,
                                      c.parent_file_id AS ParentFileId,
                                      ds.data_source_id AS DataSourceId,
                                      ds.data_source_type AS DataSourceType,
                                      f.absolute_path AS AbsolutePath,
                                      f.file_name AS FileName,
                                      f.relative_path AS RelativePath,
                                      f.file_type AS FileType,
                                      c.page_number AS PageNumber,
                                      c.chunk_index AS ChunkIndex,
                                      c.chunk_text AS ChunkText,
                                      bm25(embedding_chunks_fts) AS Score,
                                      f.fingerprint AS Fingerprint,
                                      f.file_size AS FileSize,
                                      f.creation_utc AS CreationUtc,
                                      f.last_write_utc AS LastWriteUtc,
                                      c.embedded_at_utc AS EmbeddedAtUtc,
                                      f.chunk_count AS ChunkCount
                                  FROM embedding_chunks_fts
                                  JOIN embedding_chunks c ON c.id = embedding_chunks_fts.rowid
                                  JOIN embedded_files f ON f.parent_file_id = c.parent_file_id
                                  JOIN data_sources ds ON ds.data_source_id = f.data_source_id
                                  WHERE ds.data_source_id = {dataSourceId}
                                    AND embedding_chunks_fts MATCH {ftsQuery}
                                  ORDER BY Score
                                  LIMIT {maxMatches}
                                  """)
            .AsNoTracking()
            .ToListAsync(token);

        return results.Select(ToSearchResult).ToList();
    }

    public override async Task DeleteDataSourceAsync(string dataSourceId, CancellationToken token)
    {
        await using var context = this.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(token);

        var parentFileIds = await context.EmbeddedFiles
            .Where(file => file.DataSourceId == dataSourceId)
            .Select(file => file.ParentFileId)
            .ToListAsync(token);

        foreach (var parentFileIdBatch in parentFileIds.Chunk(CHUNK_UPSERT_BATCH_SIZE))
            await context.EmbeddingChunks
                .Where(chunk => parentFileIdBatch.Contains(chunk.ParentFileId))
                .ExecuteDeleteAsync(token);

        await context.DataSources
            .Where(source => source.DataSourceId == dataSourceId)
            .ExecuteDeleteAsync(token);

        await transaction.CommitAsync(token);
    }

    public override async Task<long?> GetTotalChunkCountAsync(CancellationToken token)
    {
        try
        {
            await using var context = this.CreateContext();

            //
            // Sum the chunk counts the files carry instead of counting the rows of the chunk table:
            // embedded_files holds one row per file, embedding_chunks one per chunk. On a large index
            // that is a difference of two orders of magnitude, and the information page reads this on
            // every visit.
            //
            return await context.EmbeddedFiles.SumAsync(file => (long)file.ChunkCount, token);
        }
        catch (Exception exception)
        {
            this.Logger?.LogWarning(exception, "Failed to count the search chunks of the local RAG index.");
            return null;
        }
    }

    public override void Dispose()
    {
    }

    /// <summary>
    /// Everything the display info reads out of the database in one go.
    /// </summary>
    /// <remarks>
    /// Every property is empty when its probe could not answer. The caller turns that into "unknown".
    /// </remarks>
    private sealed record DisplaySnapshot
    {
        public string FullTextSearch { get; init; } = string.Empty;

        public string JournalMode { get; init; } = string.Empty;

        public string SchemaVersion { get; init; } = string.Empty;

        public string TableCount { get; init; } = string.Empty;

        public string DataSourceCount { get; init; } = string.Empty;

        public string FileCount { get; init; } = string.Empty;

        public string FailureCount { get; init; } = string.Empty;
    }

    private static string OrUnknown(string value) => string.IsNullOrWhiteSpace(value) ? TB("unknown") : value;

    private async Task<DisplaySnapshot> ReadDisplaySnapshotAsync()
    {
        var token = CancellationToken.None;
        try
        {
            await using var context = this.CreateContext();
            return new DisplaySnapshot
            {
                FullTextSearch = await GetFullTextSearchStateAsync(context, token),
                JournalMode = (await QueryScalarTextAsync(context, "PRAGMA journal_mode;", token)).ToUpperInvariant(),
                SchemaVersion = await GetSchemaVersionAsync(context, token),
                TableCount = await GetTableCountAsync(context, token),
                DataSourceCount = await FormatCountAsync(context.DataSources, token),
                FileCount = await FormatCountAsync(context.EmbeddedFiles, token),
                FailureCount = await FormatCountAsync(context.PermanentIndexingFailures, token),
            };
        }
        catch (Exception exception)
        {
            //
            // Opening the database failed altogether. The runtime details the caller shows next to
            // these values still say which library was loaded and for which architecture, which is
            // what a support case needs most in exactly this situation. So hand back an empty
            // snapshot instead of letting the whole block fall back.
            //
            this.Logger?.LogWarning(exception, "Failed to read the display details of the local RAG index.");
            return new DisplaySnapshot();
        }
    }

    private static async Task<string> QueryScalarTextAsync(IndexStoreDbContext context, string sql, CancellationToken token)
    {
        try
        {
            //
            // Go through the raw connection rather than through SqlQueryRaw: that one expects a
            // column named "Value" and wraps the statement, neither of which works for a PRAGMA.
            //
            var connection = context.Database.GetDbConnection();
            if (connection.State is not ConnectionState.Open)
                await connection.OpenAsync(token);

            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            var result = await command.ExecuteScalarAsync(token);
            return result?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task<string> GetFullTextSearchStateAsync(IndexStoreDbContext context, CancellationToken token)
    {
        var compiledIn = await QueryScalarTextAsync(context, "SELECT sqlite_compileoption_used('ENABLE_FTS5')", token);
        return compiledIn switch
        {
            "1" => TB("available"),
            "0" => TB("not available"),
            _ => string.Empty
        };
    }

    private static async Task<string> GetTableCountAsync(IndexStoreDbContext context, CancellationToken token)
    {
        //
        // Counts the migration history, the FTS5 virtual table and its shadow tables as well. That
        // is the point: a missing shadow table is a finding, not noise.
        //
        var tables = await QueryScalarTextAsync(context, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'", token);
        return int.TryParse(tables, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tableCount) ? tableCount.CompactCount() : string.Empty;
    }

    private static async Task<string> GetSchemaVersionAsync(IndexStoreDbContext context, CancellationToken token)
    {
        try
        {
            //
            // Reading the applied migrations only touches the history table, no assembly scan. The
            // pending ones do scan, but the schema migrator walks that same path on every start, so
            // the DynamicDependency attributes over there already keep the migration types alive.
            //
            var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync(token)).ToList();
            if (appliedMigrations.Count == 0)
                return TB("no migration applied");

            var pendingMigrations = (await context.Database.GetPendingMigrationsAsync(token)).ToList();
            return pendingMigrations.Count == 0
                ? string.Format(I18N.I.Culture, TB("{0} ({1} applied)"), appliedMigrations[^1], appliedMigrations.Count.CompactCount())
                : string.Format(I18N.I.Culture, TB("{0} ({1} applied, {2} pending)"), appliedMigrations[^1], appliedMigrations.Count.CompactCount(), pendingMigrations.Count.CompactCount());
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task<string> FormatCountAsync<T>(IQueryable<T> query, CancellationToken token) where T : class
    {
        try
        {
            return (await query.CountAsync(token)).CompactCount();
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task InitializeAsync(CancellationToken token)
    {
        await using var context = this.CreateContext();
        await IndexStoreSchemaMigrator.MigrateAsync(context, token);
    }

    private async Task<string> GetSqliteVersionAsync(CancellationToken token)
    {
        await using var context = this.CreateContext();
        var versions = await context.Database
            .SqlQueryRaw<string>("SELECT sqlite_version() AS Value")
            .ToListAsync(token);
        return versions.FirstOrDefault() ?? string.Empty;
    }

    private IndexStoreDbContext CreateContext() => new(this.dbContextOptions);

    private static void ApplyDataSource(
        EmbeddingStateDataSourceEntity dataSource,
        string dataSourceType,
        string embeddingProviderId,
        string embeddingSignature,
        string sourceHash,
        int vectorSize)
    {
        dataSource.DataSourceType = dataSourceType;
        dataSource.EmbeddingProviderId = embeddingProviderId;
        dataSource.EmbeddingSignature = embeddingSignature;
        dataSource.SourceHash = sourceHash;
        dataSource.VectorSize = vectorSize;
        dataSource.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static void ApplyFile(EmbeddingStateFileEntity fileEntity, string dataSourceId, EmbeddingStateFile file)
    {
        fileEntity.DataSourceId = dataSourceId;
        fileEntity.AbsolutePath = file.AbsolutePath;
        fileEntity.FileName = file.FileName;
        fileEntity.RelativePath = file.RelativePath;
        fileEntity.FileType = file.FileType;
        fileEntity.Fingerprint = file.Fingerprint;
        fileEntity.FileSize = file.FileSize;
        fileEntity.CreationUtc = file.CreationUtc;
        fileEntity.LastWriteUtc = file.LastWriteUtc;
        fileEntity.EmbeddedAtUtc = file.EmbeddedAtUtc;
        fileEntity.ChunkCount = file.ChunkCount;
    }

    private static void ApplyPermanentFailure(IndexingFailureEntity failureEntity, string dataSourceId, PermanentIndexingFailure failure)
    {
        failureEntity.DataSourceId = dataSourceId;
        failureEntity.AbsolutePath = failure.AbsolutePath;
        failureEntity.Fingerprint = failure.Fingerprint;
        failureEntity.FailureCode = failure.Code.ToString();
        failureEntity.FailureMessage = failure.Message;
        failureEntity.OccurredAtUtc = failure.OccurredAtUtc;
    }

    /// <remarks>
    /// A row written by a newer version may name a code this one does not know. Such a row still
    /// says that the file failed permanently, so it keeps its place in the manifest and only loses
    /// the reason it names.
    /// </remarks>
    private static FileExtractionErrorCode ParseFailureCode(string failureCode) =>
        Enum.TryParse<FileExtractionErrorCode>(failureCode, ignoreCase: true, out var parsedCode) ? parsedCode : FileExtractionErrorCode.UNKNOWN;

    private static void ApplyChunk(EmbeddingStateChunkEntity chunkEntity, EmbeddingStateChunk chunk)
    {
        chunkEntity.ChunkId = chunk.ChunkId;
        chunkEntity.ParentFileId = chunk.ParentFileId;
        chunkEntity.PageNumber = chunk.PageNumber;
        chunkEntity.ChunkIndex = chunk.ChunkIndex;
        chunkEntity.ChunkText = chunk.ChunkText;
        chunkEntity.EmbeddedAtUtc = chunk.EmbeddedAtUtc;
    }

    private static IndexStoreSearchResult ToSearchResult(IndexStoreSearchResultEntity result) => new(
        result.ChunkId,
        result.ParentFileId,
        result.DataSourceId,
        result.DataSourceType,
        result.AbsolutePath,
        result.FileName,
        result.RelativePath,
        result.FileType,
        result.PageNumber,
        result.ChunkIndex,
        result.ChunkText,
        result.Score,
        result.Fingerprint,
        result.FileSize,
        result.CreationUtc,
        result.LastWriteUtc,
        result.EmbeddedAtUtc,
        result.ChunkCount);

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

    private static NoIndexStoreClient CreateNoIndexStoreClient(string name, string? unavailableReason, DatabaseClientStatus status, ILogger<DatabaseClient> databaseClientLogger)
    {
        var client = new NoIndexStoreClient(name, unavailableReason, status);
        client.SetLogger(databaseClientLogger);
        return client;
    }
}