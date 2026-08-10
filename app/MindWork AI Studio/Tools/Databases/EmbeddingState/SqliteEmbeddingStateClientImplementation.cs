using System.Globalization;
using System.Text.RegularExpressions;

using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

using Microsoft.EntityFrameworkCore;

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
    private const int CHUNK_UPSERT_BATCH_SIZE = 500;

    private static readonly Regex FTS_TOKEN_REGEX = new(@"[\p{L}\p{Nd}_]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string databasePath = databasePath;
    private readonly DbContextOptions<EmbeddingStateDbContext> dbContextOptions = EmbeddingStateDbContext.CreateOptions(databasePath);

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
        await using var context = this.CreateContext();

        yield return (TB("Reported version"), version);
        yield return (TB("Database path"), this.databasePath);
        yield return (TB("Storage size"), this.GetStorageSize());
        yield return (TB("Indexed data sources"), (await context.DataSources.CountAsync(CancellationToken.None)).ToString(CultureInfo.InvariantCulture));
        yield return (TB("Indexed files"), (await context.EmbeddedFiles.CountAsync(CancellationToken.None)).ToString(CultureInfo.InvariantCulture));
        yield return (TB("Search chunks"), (await context.EmbeddingChunks.CountAsync(CancellationToken.None)).ToString(CultureInfo.InvariantCulture));
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

        ApplyDataSource(dataSource, dataSourceName, dataSourceType, embeddingProviderId, embeddingSignature, sourceHash, vectorSize);
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

    public override async Task<IReadOnlyList<EmbeddingStateSearchResult>> SearchChunksAsync(string dataSourceId, string query, int maxMatches, CancellationToken token)
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
                                      ds.data_source_name AS DataSourceName,
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
                                      f.chunk_count AS ChunkCount,
                                      f.compliance_level AS ComplianceLevel,
                                      f.compliance_level_rank AS ComplianceLevelRank
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

    public override void Dispose()
    {
    }

    private async Task InitializeAsync(CancellationToken token)
    {
        await using var context = this.CreateContext();
        await EmbeddingStateSchemaMigrator.MigrateAsync(context, token);
    }

    private async Task<string> GetSqliteVersionAsync(CancellationToken token)
    {
        await using var context = this.CreateContext();
        var versions = await context.Database
            .SqlQueryRaw<string>("SELECT sqlite_version() AS Value")
            .ToListAsync(token);
        return versions.FirstOrDefault() ?? string.Empty;
    }

    private EmbeddingStateDbContext CreateContext() => new(this.dbContextOptions);

    private static void ApplyDataSource(
        EmbeddingStateDataSourceEntity dataSource,
        string dataSourceName,
        string dataSourceType,
        string embeddingProviderId,
        string embeddingSignature,
        string sourceHash,
        int vectorSize)
    {
        dataSource.DataSourceName = dataSourceName;
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
        fileEntity.ComplianceLevel = file.ComplianceLevel;
        fileEntity.ComplianceLevelRank = file.ComplianceLevelRank;
    }

    private static void ApplyChunk(EmbeddingStateChunkEntity chunkEntity, EmbeddingStateChunk chunk)
    {
        chunkEntity.ChunkId = chunk.ChunkId;
        chunkEntity.ParentFileId = chunk.ParentFileId;
        chunkEntity.PageNumber = chunk.PageNumber;
        chunkEntity.ChunkIndex = chunk.ChunkIndex;
        chunkEntity.ChunkText = chunk.ChunkText;
        chunkEntity.EmbeddedAtUtc = chunk.EmbeddedAtUtc;
    }

    private static EmbeddingStateSearchResult ToSearchResult(EmbeddingStateSearchResultEntity result) => new(
        result.ChunkId,
        result.ParentFileId,
        result.DataSourceId,
        result.DataSourceName,
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
        result.ChunkCount,
        result.ComplianceLevel,
        result.ComplianceLevelRank);

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
