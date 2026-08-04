#nullable disable

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AIStudio.Tools.Databases.EmbeddingState.Migrations;

[DbContext(typeof(EmbeddingStateDbContext))]
partial class EmbeddingStateDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasAnnotation("ProductVersion", "9.0.18");
        var utcDateTimeConverter = new EmbeddingStateDateTimeConverter();

        modelBuilder.Entity("AIStudio.Tools.Databases.EmbeddingState.EmbeddingStateDataSourceEntity", entity =>
        {
            entity.Property<string>("DataSourceId")
                .HasColumnType("TEXT")
                .HasColumnName("data_source_id");

            entity.Property<string>("DataSourceName")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("data_source_name");

            entity.Property<string>("DataSourceType")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("data_source_type");

            entity.Property<string>("EmbeddingProviderId")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("embedding_provider_id");

            entity.Property<string>("EmbeddingSignature")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("embedding_signature");

            entity.Property<string>("SourceHash")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("source_hash")
                .HasDefaultValue(string.Empty);

            entity.Property<DateTime>("UpdatedAtUtc")
                .HasConversion(utcDateTimeConverter)
                .HasColumnType("TEXT")
                .HasColumnName("updated_at_utc");

            entity.Property<int>("VectorSize")
                .HasColumnType("INTEGER")
                .HasColumnName("vector_size")
                .HasDefaultValue(0);

            entity.HasKey("DataSourceId");

            entity.ToTable("data_sources");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.EmbeddingState.EmbeddingStateFileEntity", entity =>
        {
            entity.Property<string>("ParentFileId")
                .HasColumnType("TEXT")
                .HasColumnName("parent_file_id");

            entity.Property<string>("AbsolutePath")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("absolute_path")
                .UseCollation("NOCASE");

            entity.Property<int>("ChunkCount")
                .HasColumnType("INTEGER")
                .HasColumnName("chunk_count");

            entity.Property<string>("ComplianceLevel")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("compliance_level");

            entity.Property<int>("ComplianceLevelRank")
                .HasColumnType("INTEGER")
                .HasColumnName("compliance_level_rank");

            entity.Property<DateTime>("CreationUtc")
                .HasConversion(utcDateTimeConverter)
                .HasColumnType("TEXT")
                .HasColumnName("creation_utc");

            entity.Property<string>("DataSourceId")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("data_source_id");

            entity.Property<DateTime>("EmbeddedAtUtc")
                .HasConversion(utcDateTimeConverter)
                .HasColumnType("TEXT")
                .HasColumnName("embedded_at_utc");

            entity.Property<string>("FileName")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("file_name");

            entity.Property<long>("FileSize")
                .HasColumnType("INTEGER")
                .HasColumnName("file_size");

            entity.Property<string>("FileType")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("file_type");

            entity.Property<string>("Fingerprint")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("fingerprint");

            entity.Property<DateTime>("LastWriteUtc")
                .HasConversion(utcDateTimeConverter)
                .HasColumnType("TEXT")
                .HasColumnName("last_write_utc");

            entity.Property<string>("RelativePath")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("relative_path");

            entity.HasKey("ParentFileId");

            entity.HasIndex("AbsolutePath")
                .HasDatabaseName("idx_embedded_files_absolute_path");

            entity.HasIndex("ComplianceLevelRank")
                .HasDatabaseName("idx_embedded_files_compliance");

            entity.HasIndex("DataSourceId")
                .HasDatabaseName("idx_embedded_files_data_source");

            entity.HasIndex("DataSourceId", "AbsolutePath")
                .IsUnique()
                .HasDatabaseName("idx_embedded_files_data_source_absolute_path");

            entity.HasIndex("FileType")
                .HasDatabaseName("idx_embedded_files_file_type");

            entity.ToTable("embedded_files");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.EmbeddingState.EmbeddingStateChunkEntity", entity =>
        {
            entity.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER")
                .HasColumnName("id")
                .HasAnnotation("Sqlite:Autoincrement", true);

            entity.Property<string>("ChunkId")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("chunk_id");

            entity.Property<int>("ChunkIndex")
                .HasColumnType("INTEGER")
                .HasColumnName("chunk_index");

            entity.Property<string>("ChunkText")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("chunk_text");

            entity.Property<DateTime>("EmbeddedAtUtc")
                .HasConversion(utcDateTimeConverter)
                .HasColumnType("TEXT")
                .HasColumnName("embedded_at_utc");

            entity.Property<int?>("PageNumber")
                .HasColumnType("INTEGER")
                .HasColumnName("page_number");

            entity.Property<string>("ParentFileId")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("parent_file_id");

            entity.HasKey("Id");

            entity.HasIndex("ChunkId")
                .IsUnique()
                .HasDatabaseName("idx_embedding_chunks_chunk_id");

            entity.HasIndex("PageNumber")
                .HasDatabaseName("idx_embedding_chunks_page");

            entity.HasIndex("ParentFileId")
                .HasDatabaseName("idx_embedding_chunks_parent_file");

            entity.HasIndex("ParentFileId", "ChunkIndex")
                .IsUnique()
                .HasDatabaseName("idx_embedding_chunks_parent_file_chunk_index");

            entity.ToTable("embedding_chunks");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.EmbeddingState.EmbeddingStateSearchResultEntity", entity =>
        {
            entity.Property<string>("AbsolutePath")
                .IsRequired()
                .HasColumnType("TEXT");

            entity.Property<int>("ChunkCount")
                .HasColumnType("INTEGER");

            entity.Property<string>("ChunkId")
                .IsRequired()
                .HasColumnType("TEXT");

            entity.Property<int>("ChunkIndex")
                .HasColumnType("INTEGER");

            entity.Property<string>("ChunkText")
                .IsRequired()
                .HasColumnType("TEXT");

            entity.Property<string>("ComplianceLevel")
                .IsRequired()
                .HasColumnType("TEXT");

            entity.Property<int>("ComplianceLevelRank")
                .HasColumnType("INTEGER");

            entity.Property<DateTime>("CreationUtc")
                .HasConversion(utcDateTimeConverter)
                .HasColumnType("TEXT");

            entity.Property<string>("DataSourceId")
                .IsRequired()
                .HasColumnType("TEXT");

            entity.Property<string>("DataSourceName")
                .IsRequired()
                .HasColumnType("TEXT");

            entity.Property<string>("DataSourceType")
                .IsRequired()
                .HasColumnType("TEXT");

            entity.Property<DateTime>("EmbeddedAtUtc")
                .HasConversion(utcDateTimeConverter)
                .HasColumnType("TEXT");

            entity.Property<string>("FileName")
                .IsRequired()
                .HasColumnType("TEXT");

            entity.Property<long>("FileSize")
                .HasColumnType("INTEGER");

            entity.Property<string>("FileType")
                .IsRequired()
                .HasColumnType("TEXT");

            entity.Property<string>("Fingerprint")
                .IsRequired()
                .HasColumnType("TEXT");

            entity.Property<DateTime>("LastWriteUtc")
                .HasConversion(utcDateTimeConverter)
                .HasColumnType("TEXT");

            entity.Property<int?>("PageNumber")
                .HasColumnType("INTEGER");

            entity.Property<string>("ParentFileId")
                .IsRequired()
                .HasColumnType("TEXT");

            entity.Property<string>("RelativePath")
                .IsRequired()
                .HasColumnType("TEXT");

            entity.Property<double>("Score")
                .HasColumnType("REAL");

            entity.HasNoKey();

            entity.ToView("embedding_chunk_search_results");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.EmbeddingState.EmbeddingStateFileEntity", entity =>
        {
            entity.HasOne("AIStudio.Tools.Databases.EmbeddingState.EmbeddingStateDataSourceEntity", "DataSource")
                .WithMany("Files")
                .HasForeignKey("DataSourceId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            entity.Navigation("DataSource");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.EmbeddingState.EmbeddingStateChunkEntity", entity =>
        {
            entity.HasOne("AIStudio.Tools.Databases.EmbeddingState.EmbeddingStateFileEntity", "File")
                .WithMany("Chunks")
                .HasForeignKey("ParentFileId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            entity.Navigation("File");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.EmbeddingState.EmbeddingStateDataSourceEntity", entity =>
        {
            entity.Navigation("Files");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.EmbeddingState.EmbeddingStateFileEntity", entity =>
        {
            entity.Navigation("Chunks");
        });
#pragma warning restore 612, 618
    }
}
