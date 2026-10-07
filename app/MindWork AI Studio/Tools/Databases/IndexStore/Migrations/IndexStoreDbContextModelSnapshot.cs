#nullable disable

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AIStudio.Tools.Databases.IndexStore.Migrations;

// EF Core writes this file and declares the type as partial. Dropping the keyword would only
// last until the next migration is added, so the inspection is silenced instead.
// ReSharper disable once PartialTypeWithSinglePart
[DbContext(typeof(IndexStoreDbContext))]
partial class IndexStoreDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasAnnotation("ProductVersion", "9.0.18");
        var utcDateTimeOffsetConverter = new IndexStoreDateTimeOffsetConverter();

        modelBuilder.Entity("AIStudio.Tools.Databases.IndexStore.EmbeddingStateDataSourceEntity", entity =>
        {
            entity.Property<string>("DataSourceId")
                .HasColumnType("TEXT")
                .HasColumnName("data_source_id");

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

            entity.Property<DateTimeOffset>("UpdatedAtUtc")
                .HasConversion(utcDateTimeOffsetConverter)
                .HasColumnType("TEXT")
                .HasColumnName("updated_at_utc");

            entity.Property<int>("VectorSize")
                .HasColumnType("INTEGER")
                .HasColumnName("vector_size")
                .HasDefaultValue(0);

            entity.HasKey("DataSourceId");

            entity.ToTable("data_sources");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.IndexStore.EmbeddingStateFileEntity", entity =>
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

            entity.Property<DateTimeOffset>("CreationUtc")
                .HasConversion(utcDateTimeOffsetConverter)
                .HasColumnType("TEXT")
                .HasColumnName("creation_utc");

            entity.Property<string>("DataSourceId")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("data_source_id");

            entity.Property<DateTimeOffset>("EmbeddedAtUtc")
                .HasConversion(utcDateTimeOffsetConverter)
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

            entity.Property<DateTimeOffset>("LastWriteUtc")
                .HasConversion(utcDateTimeOffsetConverter)
                .HasColumnType("TEXT")
                .HasColumnName("last_write_utc");

            entity.Property<string>("RelativePath")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("relative_path");

            entity.HasKey("ParentFileId");

            entity.HasIndex("AbsolutePath")
                .HasDatabaseName("idx_embedded_files_absolute_path");

            entity.HasIndex("DataSourceId")
                .HasDatabaseName("idx_embedded_files_data_source");

            entity.HasIndex("DataSourceId", "AbsolutePath")
                .IsUnique()
                .HasDatabaseName("idx_embedded_files_data_source_absolute_path");

            entity.HasIndex("FileType")
                .HasDatabaseName("idx_embedded_files_file_type");

            entity.ToTable("embedded_files");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.IndexStore.EmbeddingStateChunkEntity", entity =>
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

            entity.Property<DateTimeOffset>("EmbeddedAtUtc")
                .HasConversion(utcDateTimeOffsetConverter)
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

        modelBuilder.Entity("AIStudio.Tools.Databases.IndexStore.IndexingFailureEntity", entity =>
        {
            entity.Property<string>("ParentFileId")
                .HasColumnType("TEXT")
                .HasColumnName("parent_file_id");

            entity.Property<string>("AbsolutePath")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("absolute_path")
                .UseCollation("NOCASE");

            entity.Property<string>("DataSourceId")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("data_source_id");

            entity.Property<string>("FailureCode")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("failure_code");

            entity.Property<string>("FailureMessage")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("failure_message");

            entity.Property<string>("Fingerprint")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("fingerprint");

            entity.Property<DateTimeOffset>("OccurredAtUtc")
                .HasConversion(utcDateTimeOffsetConverter)
                .HasColumnType("TEXT")
                .HasColumnName("occurred_at_utc");

            entity.HasKey("ParentFileId");

            entity.HasIndex("DataSourceId")
                .HasDatabaseName("idx_permanent_indexing_failures_data_source");

            entity.HasIndex("DataSourceId", "AbsolutePath")
                .IsUnique()
                .HasDatabaseName("idx_permanent_indexing_failures_data_source_absolute_path");

            entity.ToTable("permanent_indexing_failures");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.IndexStore.IndexStoreSearchResultEntity", entity =>
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

            entity.Property<DateTimeOffset>("CreationUtc")
                .HasConversion(utcDateTimeOffsetConverter)
                .HasColumnType("TEXT");

            entity.Property<string>("DataSourceId")
                .IsRequired()
                .HasColumnType("TEXT");

            entity.Property<string>("DataSourceType")
                .IsRequired()
                .HasColumnType("TEXT");

            entity.Property<DateTimeOffset>("EmbeddedAtUtc")
                .HasConversion(utcDateTimeOffsetConverter)
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

            entity.Property<DateTimeOffset>("LastWriteUtc")
                .HasConversion(utcDateTimeOffsetConverter)
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

        modelBuilder.Entity("AIStudio.Tools.Databases.IndexStore.MailAddressEntity", entity =>
        {
            entity.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER")
                .HasColumnName("id")
                .HasAnnotation("Sqlite:Autoincrement", true);

            entity.Property<string>("Address")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("address")
                .UseCollation("NOCASE");

            entity.Property<string>("DisplayName")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("display_name");

            entity.Property<string>("ParentFileId")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("parent_file_id");

            entity.Property<int>("Position")
                .HasColumnType("INTEGER")
                .HasColumnName("position");

            entity.Property<string>("Role")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("role");

            entity.HasKey("Id");

            entity.HasIndex("Address")
                .HasDatabaseName("idx_mail_addresses_address");

            entity.HasIndex("ParentFileId", "Role", "Position")
                .IsUnique()
                .HasDatabaseName("idx_mail_addresses_parent_file_role_position");

            entity.ToTable("mail_addresses");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.IndexStore.MailFolderEntity", entity =>
        {
            entity.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER")
                .HasColumnName("id")
                .HasAnnotation("Sqlite:Autoincrement", true);

            entity.Property<string>("DataSourceId")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("data_source_id");

            entity.Property<long?>("HighestModSeq")
                .HasColumnType("INTEGER")
                .HasColumnName("highest_mod_seq");

            entity.Property<DateTimeOffset?>("InitialSyncCompletedUtc")
                .HasConversion(utcDateTimeOffsetConverter)
                .HasColumnType("TEXT")
                .HasColumnName("initial_sync_completed_utc");

            entity.Property<string>("Path")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("path");

            entity.Property<long?>("ServerMessageCount")
                .HasColumnType("INTEGER")
                .HasColumnName("server_message_count");

            entity.Property<long?>("ServerUnseenCount")
                .HasColumnType("INTEGER")
                .HasColumnName("server_unseen_count");

            entity.Property<string>("SpecialUse")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("special_use");

            entity.Property<long?>("UidNext")
                .HasColumnType("INTEGER")
                .HasColumnName("uid_next");

            entity.Property<long>("UidValidity")
                .HasColumnType("INTEGER")
                .HasColumnName("uid_validity");

            entity.HasKey("Id");

            entity.HasIndex("DataSourceId", "Path")
                .IsUnique()
                .HasDatabaseName("idx_mail_folders_data_source_path");

            entity.ToTable("mail_folders");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.IndexStore.MailLocationEntity", entity =>
        {
            entity.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER")
                .HasColumnName("id")
                .HasAnnotation("Sqlite:Autoincrement", true);

            entity.Property<int>("FolderId")
                .HasColumnType("INTEGER")
                .HasColumnName("folder_id");

            entity.Property<bool>("IsAnswered")
                .HasColumnType("INTEGER")
                .HasColumnName("is_answered");

            entity.Property<bool>("IsFlagged")
                .HasColumnType("INTEGER")
                .HasColumnName("is_flagged");

            entity.Property<bool>("IsSeen")
                .HasColumnType("INTEGER")
                .HasColumnName("is_seen");

            entity.Property<string>("ParentFileId")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("parent_file_id");

            entity.Property<long>("Uid")
                .HasColumnType("INTEGER")
                .HasColumnName("uid");

            entity.HasKey("Id");

            entity.HasIndex("ParentFileId")
                .HasDatabaseName("idx_mail_locations_parent_file");

            entity.HasIndex("FolderId", "Uid")
                .IsUnique()
                .HasDatabaseName("idx_mail_locations_folder_uid");

            entity.ToTable("mail_locations");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.IndexStore.MailMessageEntity", entity =>
        {
            entity.Property<string>("ParentFileId")
                .HasColumnType("TEXT")
                .HasColumnName("parent_file_id");

            entity.Property<string>("DataSourceId")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("data_source_id");

            entity.Property<string>("EncryptionKind")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("encryption_kind");

            entity.Property<DateTimeOffset>("FirstSeenUtc")
                .HasConversion(utcDateTimeOffsetConverter)
                .HasColumnType("TEXT")
                .HasColumnName("first_seen_utc");

            entity.Property<string>("Importance")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("importance");

            entity.Property<string>("InReplyTo")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("in_reply_to");

            entity.Property<string>("MailHash")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("mail_hash");

            entity.Property<string>("MessageId")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("message_id");

            entity.Property<DateTimeOffset?>("OrphanedAtUtc")
                .HasConversion(utcDateTimeOffsetConverter)
                .HasColumnType("TEXT")
                .HasColumnName("orphaned_at_utc");

            entity.Property<DateTimeOffset>("ReceivedAtUtc")
                .HasConversion(utcDateTimeOffsetConverter)
                .HasColumnType("TEXT")
                .HasColumnName("received_at_utc");

            entity.Property<string>("ReferenceMessageIds")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("reference_message_ids");

            entity.Property<DateTimeOffset?>("SentAtUtc")
                .HasConversion(utcDateTimeOffsetConverter)
                .HasColumnType("TEXT")
                .HasColumnName("sent_at_utc");

            entity.HasKey("ParentFileId");

            entity.HasIndex("DataSourceId", "MessageId")
                .HasDatabaseName("idx_mail_messages_data_source_message_id");

            entity.HasIndex("DataSourceId", "ReceivedAtUtc")
                .HasDatabaseName("idx_mail_messages_data_source_received");

            entity.ToTable("mail_messages");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.IndexStore.MailPartEntity", entity =>
        {
            entity.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER")
                .HasColumnName("id")
                .HasAnnotation("Sqlite:Autoincrement", true);

            entity.Property<string>("ContentType")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("content_type");

            entity.Property<string>("Kind")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("kind");

            entity.Property<string>("Name")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("name");

            entity.Property<string>("ParentFileId")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("parent_file_id");

            entity.Property<long>("PartSize")
                .HasColumnType("INTEGER")
                .HasColumnName("part_size");

            entity.Property<int>("Position")
                .HasColumnType("INTEGER")
                .HasColumnName("position");

            entity.Property<string>("Text")
                .HasColumnType("TEXT")
                .HasColumnName("text");

            entity.Property<string>("TextState")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("text_state");

            entity.HasKey("Id");

            entity.HasIndex("ParentFileId", "Kind", "Position")
                .IsUnique()
                .HasDatabaseName("idx_mail_parts_parent_file_kind_position");

            entity.ToTable("mail_parts");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.IndexStore.MailboxAuthStateEntity", entity =>
        {
            entity.Property<string>("DataSourceId")
                .HasColumnType("TEXT")
                .HasColumnName("data_source_id");

            entity.Property<DateTimeOffset>("FailedAtUtc")
                .HasConversion(utcDateTimeOffsetConverter)
                .HasColumnType("TEXT")
                .HasColumnName("failed_at_utc");

            entity.Property<string>("FailureMessage")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("failure_message");

            entity.HasKey("DataSourceId");

            entity.ToTable("mailbox_auth_state");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.IndexStore.MailboxSyncStateEntity", entity =>
        {
            entity.Property<string>("DataSourceId")
                .HasColumnType("TEXT")
                .HasColumnName("data_source_id");

            entity.Property<DateTimeOffset?>("LastSyncCompletedUtc")
                .HasConversion(utcDateTimeOffsetConverter)
                .HasColumnType("TEXT")
                .HasColumnName("last_sync_completed_utc");

            entity.Property<DateTimeOffset?>("PendingRemovalApprovedUtc")
                .HasConversion(utcDateTimeOffsetConverter)
                .HasColumnType("TEXT")
                .HasColumnName("pending_removal_approved_utc");

            entity.Property<int?>("PendingRemovalCount")
                .HasColumnType("INTEGER")
                .HasColumnName("pending_removal_count");

            entity.HasKey("DataSourceId");

            entity.ToTable("mailbox_sync_state");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.IndexStore.EmbeddingStateFileEntity", entity =>
        {
            entity.HasOne("AIStudio.Tools.Databases.IndexStore.EmbeddingStateDataSourceEntity", "DataSource")
                .WithMany("Files")
                .HasForeignKey("DataSourceId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            entity.Navigation("DataSource");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.IndexStore.EmbeddingStateChunkEntity", entity =>
        {
            entity.HasOne("AIStudio.Tools.Databases.IndexStore.EmbeddingStateFileEntity", "File")
                .WithMany("Chunks")
                .HasForeignKey("ParentFileId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            entity.Navigation("File");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.IndexStore.IndexingFailureEntity", entity =>
        {
            entity.HasOne("AIStudio.Tools.Databases.IndexStore.EmbeddingStateDataSourceEntity", "DataSource")
                .WithMany("PermanentIndexingFailures")
                .HasForeignKey("DataSourceId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            entity.Navigation("DataSource");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.IndexStore.MailAddressEntity", entity =>
        {
            entity.HasOne("AIStudio.Tools.Databases.IndexStore.MailMessageEntity", "Message")
                .WithMany("Addresses")
                .HasForeignKey("ParentFileId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            entity.Navigation("Message");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.IndexStore.MailFolderEntity", entity =>
        {
            entity.HasOne("AIStudio.Tools.Databases.IndexStore.EmbeddingStateDataSourceEntity", null)
                .WithMany()
                .HasForeignKey("DataSourceId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.IndexStore.MailLocationEntity", entity =>
        {
            entity.HasOne("AIStudio.Tools.Databases.IndexStore.MailFolderEntity", "Folder")
                .WithMany("Locations")
                .HasForeignKey("FolderId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            entity.HasOne("AIStudio.Tools.Databases.IndexStore.MailMessageEntity", "Message")
                .WithMany("Locations")
                .HasForeignKey("ParentFileId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            entity.Navigation("Folder");

            entity.Navigation("Message");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.IndexStore.MailMessageEntity", entity =>
        {
            entity.HasOne("AIStudio.Tools.Databases.IndexStore.EmbeddingStateDataSourceEntity", null)
                .WithMany()
                .HasForeignKey("DataSourceId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            entity.HasOne("AIStudio.Tools.Databases.IndexStore.EmbeddingStateFileEntity", null)
                .WithOne()
                .HasForeignKey("AIStudio.Tools.Databases.IndexStore.MailMessageEntity", "ParentFileId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.IndexStore.MailPartEntity", entity =>
        {
            entity.HasOne("AIStudio.Tools.Databases.IndexStore.MailMessageEntity", "Message")
                .WithMany("Parts")
                .HasForeignKey("ParentFileId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            entity.Navigation("Message");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.IndexStore.MailboxSyncStateEntity", entity =>
        {
            entity.HasOne("AIStudio.Tools.Databases.IndexStore.EmbeddingStateDataSourceEntity", null)
                .WithOne()
                .HasForeignKey("AIStudio.Tools.Databases.IndexStore.MailboxSyncStateEntity", "DataSourceId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.IndexStore.EmbeddingStateDataSourceEntity", entity =>
        {
            entity.Navigation("Files");

            entity.Navigation("PermanentIndexingFailures");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.IndexStore.EmbeddingStateFileEntity", entity =>
        {
            entity.Navigation("Chunks");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.IndexStore.MailFolderEntity", entity =>
        {
            entity.Navigation("Locations");
        });

        modelBuilder.Entity("AIStudio.Tools.Databases.IndexStore.MailMessageEntity", entity =>
        {
            entity.Navigation("Addresses");

            entity.Navigation("Locations");

            entity.Navigation("Parts");
        });
#pragma warning restore 612, 618
    }
}
