using System.Globalization;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AIStudio.Tools.Databases.EmbeddingState;

internal sealed class EmbeddingStateDbContext(DbContextOptions<EmbeddingStateDbContext> options) : DbContext(options)
{
    public static DbContextOptions<EmbeddingStateDbContext> CreateOptions(string databasePath) => new DbContextOptionsBuilder<EmbeddingStateDbContext>()
        .UseSqlite(BuildConnectionString(databasePath))
        .Options;

    public DbSet<EmbeddingStateDataSourceEntity> DataSources => this.Set<EmbeddingStateDataSourceEntity>();

    public DbSet<EmbeddingStateFileEntity> EmbeddedFiles => this.Set<EmbeddingStateFileEntity>();

    public DbSet<EmbeddingStateChunkEntity> EmbeddingChunks => this.Set<EmbeddingStateChunkEntity>();

    public DbSet<EmbeddingStateSearchResultEntity> SearchResults => this.Set<EmbeddingStateSearchResultEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var utcDateTimeConverter = new EmbeddingStateDateTimeConverter();

        modelBuilder.Entity<EmbeddingStateDataSourceEntity>(entity =>
        {
            entity.ToTable("data_sources");
            entity.HasKey(dataSource => dataSource.DataSourceId);

            entity.Property(dataSource => dataSource.DataSourceId).HasColumnName("data_source_id");
            entity.Property(dataSource => dataSource.DataSourceName).HasColumnName("data_source_name").IsRequired();
            entity.Property(dataSource => dataSource.DataSourceType).HasColumnName("data_source_type").IsRequired();
            entity.Property(dataSource => dataSource.EmbeddingProviderId).HasColumnName("embedding_provider_id").IsRequired();
            entity.Property(dataSource => dataSource.EmbeddingSignature).HasColumnName("embedding_signature").IsRequired();
            entity.Property(dataSource => dataSource.SourceHash).HasColumnName("source_hash").IsRequired().HasDefaultValue(string.Empty);
            entity.Property(dataSource => dataSource.VectorSize).HasColumnName("vector_size").HasDefaultValue(0);
            entity.Property(dataSource => dataSource.UpdatedAtUtc).HasColumnName("updated_at_utc").HasConversion(utcDateTimeConverter).IsRequired();

            entity
                .HasMany(dataSource => dataSource.Files)
                .WithOne(file => file.DataSource)
                .HasForeignKey(file => file.DataSourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmbeddingStateFileEntity>(entity =>
        {
            entity.ToTable("embedded_files");
            entity.HasKey(file => file.ParentFileId);

            entity.Property(file => file.ParentFileId).HasColumnName("parent_file_id");
            entity.Property(file => file.DataSourceId).HasColumnName("data_source_id").IsRequired();
            entity.Property(file => file.AbsolutePath).HasColumnName("absolute_path").UseCollation("NOCASE").IsRequired();
            entity.Property(file => file.FileName).HasColumnName("file_name").IsRequired();
            entity.Property(file => file.RelativePath).HasColumnName("relative_path").IsRequired();
            entity.Property(file => file.FileType).HasColumnName("file_type").IsRequired();
            entity.Property(file => file.Fingerprint).HasColumnName("fingerprint").IsRequired();
            entity.Property(file => file.FileSize).HasColumnName("file_size");
            entity.Property(file => file.CreationUtc).HasColumnName("creation_utc").HasConversion(utcDateTimeConverter).IsRequired();
            entity.Property(file => file.LastWriteUtc).HasColumnName("last_write_utc").HasConversion(utcDateTimeConverter).IsRequired();
            entity.Property(file => file.EmbeddedAtUtc).HasColumnName("embedded_at_utc").HasConversion(utcDateTimeConverter).IsRequired();
            entity.Property(file => file.ChunkCount).HasColumnName("chunk_count");
            entity.Property(file => file.ComplianceLevel).HasColumnName("compliance_level").IsRequired();
            entity.Property(file => file.ComplianceLevelRank).HasColumnName("compliance_level_rank");

            entity.HasIndex(file => file.DataSourceId).HasDatabaseName("idx_embedded_files_data_source");
            entity.HasIndex(file => file.AbsolutePath).HasDatabaseName("idx_embedded_files_absolute_path");
            entity.HasIndex(file => file.FileType).HasDatabaseName("idx_embedded_files_file_type");
            entity.HasIndex(file => file.ComplianceLevelRank).HasDatabaseName("idx_embedded_files_compliance");
            entity.HasIndex(file => new { file.DataSourceId, file.AbsolutePath }).HasDatabaseName("idx_embedded_files_data_source_absolute_path").IsUnique();

            entity
                .HasMany(file => file.Chunks)
                .WithOne(chunk => chunk.File)
                .HasForeignKey(chunk => chunk.ParentFileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmbeddingStateChunkEntity>(entity =>
        {
            entity.ToTable("embedding_chunks");
            entity.HasKey(chunk => chunk.Id);

            entity.Property(chunk => chunk.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(chunk => chunk.ChunkId).HasColumnName("chunk_id").IsRequired();
            entity.Property(chunk => chunk.ParentFileId).HasColumnName("parent_file_id").IsRequired();
            entity.Property(chunk => chunk.PageNumber).HasColumnName("page_number");
            entity.Property(chunk => chunk.ChunkIndex).HasColumnName("chunk_index");
            entity.Property(chunk => chunk.ChunkText).HasColumnName("chunk_text").IsRequired();
            entity.Property(chunk => chunk.EmbeddedAtUtc).HasColumnName("embedded_at_utc").HasConversion(utcDateTimeConverter).IsRequired();

            entity.HasIndex(chunk => chunk.ChunkId).HasDatabaseName("idx_embedding_chunks_chunk_id").IsUnique();
            entity.HasIndex(chunk => chunk.ParentFileId).HasDatabaseName("idx_embedding_chunks_parent_file");
            entity.HasIndex(chunk => chunk.PageNumber).HasDatabaseName("idx_embedding_chunks_page");
            entity.HasIndex(chunk => new { chunk.ParentFileId, chunk.ChunkIndex }).HasDatabaseName("idx_embedding_chunks_parent_file_chunk_index").IsUnique();
        });

        modelBuilder.Entity<EmbeddingStateSearchResultEntity>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("embedding_chunk_search_results");

            entity.Property(result => result.CreationUtc).HasConversion(utcDateTimeConverter);
            entity.Property(result => result.LastWriteUtc).HasConversion(utcDateTimeConverter);
            entity.Property(result => result.EmbeddedAtUtc).HasConversion(utcDateTimeConverter);
        });
    }

    private static string BuildConnectionString(string databasePath) => new SqliteConnectionStringBuilder
    {
        DataSource = databasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Shared,
        ForeignKeys = true,
        DefaultTimeout = 30,
    }.ToString();
}

internal sealed class EmbeddingStateDataSourceEntity
{
    public string DataSourceId { get; set; } = string.Empty;

    public string DataSourceName { get; set; } = string.Empty;

    public string DataSourceType { get; set; } = string.Empty;

    public string EmbeddingProviderId { get; set; } = string.Empty;

    public string EmbeddingSignature { get; set; } = string.Empty;

    public string SourceHash { get; set; } = string.Empty;

    public int VectorSize { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public List<EmbeddingStateFileEntity> Files { get; set; } = [];
}

internal sealed class EmbeddingStateFileEntity
{
    public string ParentFileId { get; set; } = string.Empty;

    public string DataSourceId { get; set; } = string.Empty;

    public string AbsolutePath { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;

    public string Fingerprint { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public DateTime CreationUtc { get; set; }

    public DateTime LastWriteUtc { get; set; }

    public DateTime EmbeddedAtUtc { get; set; }

    public int ChunkCount { get; set; }

    public string ComplianceLevel { get; set; } = string.Empty;

    public int ComplianceLevelRank { get; set; }

    public EmbeddingStateDataSourceEntity? DataSource { get; set; }

    public List<EmbeddingStateChunkEntity> Chunks { get; set; } = [];
}

internal sealed class EmbeddingStateChunkEntity
{
    public int Id { get; set; }

    public string ChunkId { get; set; } = string.Empty;

    public string ParentFileId { get; set; } = string.Empty;

    public int? PageNumber { get; set; }

    public int ChunkIndex { get; set; }

    public string ChunkText { get; set; } = string.Empty;

    public DateTime EmbeddedAtUtc { get; set; }

    public EmbeddingStateFileEntity? File { get; set; }
}

internal sealed class EmbeddingStateSearchResultEntity
{
    public string ChunkId { get; set; } = string.Empty;

    public string ParentFileId { get; set; } = string.Empty;

    public string DataSourceId { get; set; } = string.Empty;

    public string DataSourceName { get; set; } = string.Empty;

    public string DataSourceType { get; set; } = string.Empty;

    public string AbsolutePath { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;

    public int? PageNumber { get; set; }

    public int ChunkIndex { get; set; }

    public string ChunkText { get; set; } = string.Empty;

    public double Score { get; set; }

    public string Fingerprint { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public DateTime CreationUtc { get; set; }

    public DateTime LastWriteUtc { get; set; }

    public DateTime EmbeddedAtUtc { get; set; }

    public int ChunkCount { get; set; }

    public string ComplianceLevel { get; set; } = string.Empty;

    public int ComplianceLevelRank { get; set; }
}

internal sealed class EmbeddingStateDateTimeConverter() : ValueConverter<DateTime, string>(
    value => EmbeddingStateDateTime.ToUtcText(value),
    value => EmbeddingStateDateTime.ParseUtc(value));

internal static class EmbeddingStateDateTime
{
    public static string ToUtcText(DateTime dateTime)
    {
        var utc = dateTime.Kind is DateTimeKind.Utc ? dateTime : dateTime.ToUniversalTime();
        return utc.ToString("O", CultureInfo.InvariantCulture);
    }

    public static DateTime ParseUtc(string value)
    {
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTime)
            ? dateTime.ToUniversalTime()
            : DateTime.UnixEpoch;
    }
}
