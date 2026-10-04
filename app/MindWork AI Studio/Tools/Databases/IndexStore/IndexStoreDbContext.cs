using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AIStudio.Tools.Databases.IndexStore;

internal sealed class IndexStoreDbContext(DbContextOptions<IndexStoreDbContext> options) : DbContext(options)
{
    public static DbContextOptions<IndexStoreDbContext> CreateOptions(string databasePath) => new DbContextOptionsBuilder<IndexStoreDbContext>()
        .UseSqlite(BuildConnectionString(databasePath))
        .Options;

    public DbSet<EmbeddingStateDataSourceEntity> DataSources => this.Set<EmbeddingStateDataSourceEntity>();

    public DbSet<EmbeddingStateFileEntity> EmbeddedFiles => this.Set<EmbeddingStateFileEntity>();

    public DbSet<EmbeddingStateChunkEntity> EmbeddingChunks => this.Set<EmbeddingStateChunkEntity>();

    public DbSet<IndexingFailureEntity> PermanentIndexingFailures => this.Set<IndexingFailureEntity>();

    public DbSet<IndexStoreSearchResultEntity> SearchResults => this.Set<IndexStoreSearchResultEntity>();

    public DbSet<MailMessageEntity> MailMessages => this.Set<MailMessageEntity>();

    public DbSet<MailAddressEntity> MailAddresses => this.Set<MailAddressEntity>();

    public DbSet<MailPartEntity> MailParts => this.Set<MailPartEntity>();

    public DbSet<MailFolderEntity> MailFolders => this.Set<MailFolderEntity>();

    public DbSet<MailLocationEntity> MailLocations => this.Set<MailLocationEntity>();

    public DbSet<MailboxSyncStateEntity> MailboxSyncStates => this.Set<MailboxSyncStateEntity>();

    public DbSet<MailboxAuthStateEntity> MailboxAuthStates => this.Set<MailboxAuthStateEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var utcDateTimeOffsetConverter = new IndexStoreDateTimeOffsetConverter();

        modelBuilder.Entity<EmbeddingStateDataSourceEntity>(entity =>
        {
            entity.ToTable("data_sources");
            entity.HasKey(dataSource => dataSource.DataSourceId);

            entity.Property(dataSource => dataSource.DataSourceId).HasColumnName("data_source_id");
            entity.Property(dataSource => dataSource.DataSourceType).HasColumnName("data_source_type").IsRequired();
            entity.Property(dataSource => dataSource.EmbeddingProviderId).HasColumnName("embedding_provider_id").IsRequired();
            entity.Property(dataSource => dataSource.EmbeddingSignature).HasColumnName("embedding_signature").IsRequired();
            entity.Property(dataSource => dataSource.SourceHash).HasColumnName("source_hash").IsRequired().HasDefaultValue(string.Empty);
            entity.Property(dataSource => dataSource.VectorSize).HasColumnName("vector_size").HasDefaultValue(0);
            entity.Property(dataSource => dataSource.UpdatedAtUtc).HasColumnName("updated_at_utc").HasConversion(utcDateTimeOffsetConverter).IsRequired();

            entity
                .HasMany(dataSource => dataSource.Files)
                .WithOne(file => file.DataSource)
                .HasForeignKey(file => file.DataSourceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasMany(dataSource => dataSource.PermanentIndexingFailures)
                .WithOne(failure => failure.DataSource)
                .HasForeignKey(failure => failure.DataSourceId)
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
            entity.Property(file => file.CreationUtc).HasColumnName("creation_utc").HasConversion(utcDateTimeOffsetConverter).IsRequired();
            entity.Property(file => file.LastWriteUtc).HasColumnName("last_write_utc").HasConversion(utcDateTimeOffsetConverter).IsRequired();
            entity.Property(file => file.EmbeddedAtUtc).HasColumnName("embedded_at_utc").HasConversion(utcDateTimeOffsetConverter).IsRequired();
            entity.Property(file => file.ChunkCount).HasColumnName("chunk_count");

            entity.HasIndex(file => file.DataSourceId).HasDatabaseName("idx_embedded_files_data_source");
            entity.HasIndex(file => file.AbsolutePath).HasDatabaseName("idx_embedded_files_absolute_path");
            entity.HasIndex(file => file.FileType).HasDatabaseName("idx_embedded_files_file_type");
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
            entity.Property(chunk => chunk.EmbeddedAtUtc).HasColumnName("embedded_at_utc").HasConversion(utcDateTimeOffsetConverter).IsRequired();

            entity.HasIndex(chunk => chunk.ChunkId).HasDatabaseName("idx_embedding_chunks_chunk_id").IsUnique();
            entity.HasIndex(chunk => chunk.ParentFileId).HasDatabaseName("idx_embedding_chunks_parent_file");
            entity.HasIndex(chunk => chunk.PageNumber).HasDatabaseName("idx_embedding_chunks_page");
            entity.HasIndex(chunk => new { chunk.ParentFileId, chunk.ChunkIndex }).HasDatabaseName("idx_embedding_chunks_parent_file_chunk_index").IsUnique();
        });

        modelBuilder.Entity<IndexingFailureEntity>(entity =>
        {
            entity.ToTable("permanent_indexing_failures");
            entity.HasKey(failure => failure.ParentFileId);

            entity.Property(failure => failure.ParentFileId).HasColumnName("parent_file_id");
            entity.Property(failure => failure.DataSourceId).HasColumnName("data_source_id").IsRequired();
            entity.Property(failure => failure.AbsolutePath).HasColumnName("absolute_path").UseCollation("NOCASE").IsRequired();
            entity.Property(failure => failure.Fingerprint).HasColumnName("fingerprint").IsRequired();
            entity.Property(failure => failure.FailureCode).HasColumnName("failure_code").IsRequired();
            entity.Property(failure => failure.FailureMessage).HasColumnName("failure_message").IsRequired();
            entity.Property(failure => failure.OccurredAtUtc).HasColumnName("occurred_at_utc").HasConversion(utcDateTimeOffsetConverter).IsRequired();

            entity.HasIndex(failure => failure.DataSourceId).HasDatabaseName("idx_permanent_indexing_failures_data_source");
            entity.HasIndex(failure => new { failure.DataSourceId, failure.AbsolutePath }).HasDatabaseName("idx_permanent_indexing_failures_data_source_absolute_path").IsUnique();
        });

        modelBuilder.Entity<IndexStoreSearchResultEntity>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("embedding_chunk_search_results");

            entity.Property(result => result.CreationUtc).HasConversion(utcDateTimeOffsetConverter);
            entity.Property(result => result.LastWriteUtc).HasConversion(utcDateTimeOffsetConverter);
            entity.Property(result => result.EmbeddedAtUtc).HasConversion(utcDateTimeOffsetConverter);
        });

        modelBuilder.Entity<MailMessageEntity>(entity =>
        {
            entity.ToTable("mail_messages");
            entity.HasKey(mail => mail.ParentFileId);

            entity.Property(mail => mail.ParentFileId).HasColumnName("parent_file_id");
            entity.Property(mail => mail.DataSourceId).HasColumnName("data_source_id").IsRequired();
            entity.Property(mail => mail.MessageId).HasColumnName("message_id").IsRequired();
            entity.Property(mail => mail.InReplyTo).HasColumnName("in_reply_to").IsRequired();
            entity.Property(mail => mail.ReferenceMessageIds).HasColumnName("reference_message_ids").IsRequired();
            entity.Property(mail => mail.SentAtUtc).HasColumnName("sent_at_utc").HasConversion(utcDateTimeOffsetConverter);
            entity.Property(mail => mail.ReceivedAtUtc).HasColumnName("received_at_utc").HasConversion(utcDateTimeOffsetConverter).IsRequired();
            entity.Property(mail => mail.Importance).HasColumnName("importance").IsRequired();
            entity.Property(mail => mail.EncryptionKind).HasColumnName("encryption_kind").IsRequired();
            entity.Property(mail => mail.MailHash).HasColumnName("mail_hash").IsRequired();
            entity.Property(mail => mail.FirstSeenUtc).HasColumnName("first_seen_utc").HasConversion(utcDateTimeOffsetConverter).IsRequired();
            entity.Property(mail => mail.OrphanedAtUtc).HasColumnName("orphaned_at_utc").HasConversion(utcDateTimeOffsetConverter);

            entity.HasIndex(mail => new { mail.DataSourceId, mail.ReceivedAtUtc }).HasDatabaseName("idx_mail_messages_data_source_received");
            entity.HasIndex(mail => new { mail.DataSourceId, mail.MessageId }).HasDatabaseName("idx_mail_messages_data_source_message_id");

            entity
                .HasOne<EmbeddingStateFileEntity>()
                .WithOne()
                .HasForeignKey<MailMessageEntity>(mail => mail.ParentFileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne<EmbeddingStateDataSourceEntity>()
                .WithMany()
                .HasForeignKey(mail => mail.DataSourceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasMany(mail => mail.Addresses)
                .WithOne(address => address.Message)
                .HasForeignKey(address => address.ParentFileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasMany(mail => mail.Parts)
                .WithOne(part => part.Message)
                .HasForeignKey(part => part.ParentFileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasMany(mail => mail.Locations)
                .WithOne(location => location.Message)
                .HasForeignKey(location => location.ParentFileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MailAddressEntity>(entity =>
        {
            entity.ToTable("mail_addresses");
            entity.HasKey(address => address.Id);

            entity.Property(address => address.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(address => address.ParentFileId).HasColumnName("parent_file_id").IsRequired();
            entity.Property(address => address.Role).HasColumnName("role").IsRequired();
            entity.Property(address => address.Position).HasColumnName("position");
            entity.Property(address => address.Address).HasColumnName("address").UseCollation("NOCASE").IsRequired();
            entity.Property(address => address.DisplayName).HasColumnName("display_name").IsRequired();

            entity.HasIndex(address => new { address.ParentFileId, address.Role, address.Position }).HasDatabaseName("idx_mail_addresses_parent_file_role_position").IsUnique();
            entity.HasIndex(address => address.Address).HasDatabaseName("idx_mail_addresses_address");
        });

        modelBuilder.Entity<MailPartEntity>(entity =>
        {
            entity.ToTable("mail_parts");
            entity.HasKey(part => part.Id);

            entity.Property(part => part.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(part => part.ParentFileId).HasColumnName("parent_file_id").IsRequired();
            entity.Property(part => part.Kind).HasColumnName("kind").IsRequired();
            entity.Property(part => part.Position).HasColumnName("position");
            entity.Property(part => part.Name).HasColumnName("name").IsRequired();
            entity.Property(part => part.ContentType).HasColumnName("content_type").IsRequired();
            entity.Property(part => part.PartSize).HasColumnName("part_size");
            entity.Property(part => part.Text).HasColumnName("text");
            entity.Property(part => part.TextState).HasColumnName("text_state").IsRequired();

            entity.HasIndex(part => new { part.ParentFileId, part.Kind, part.Position }).HasDatabaseName("idx_mail_parts_parent_file_kind_position").IsUnique();
        });

        modelBuilder.Entity<MailFolderEntity>(entity =>
        {
            entity.ToTable("mail_folders");
            entity.HasKey(folder => folder.Id);

            entity.Property(folder => folder.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(folder => folder.DataSourceId).HasColumnName("data_source_id").IsRequired();
            entity.Property(folder => folder.Path).HasColumnName("path").IsRequired();
            entity.Property(folder => folder.SpecialUse).HasColumnName("special_use").IsRequired();
            entity.Property(folder => folder.UidValidity).HasColumnName("uid_validity");
            entity.Property(folder => folder.UidNext).HasColumnName("uid_next");
            entity.Property(folder => folder.HighestModSeq).HasColumnName("highest_mod_seq");
            entity.Property(folder => folder.ServerMessageCount).HasColumnName("server_message_count");
            entity.Property(folder => folder.ServerUnseenCount).HasColumnName("server_unseen_count");
            entity.Property(folder => folder.InitialSyncCompletedUtc).HasColumnName("initial_sync_completed_utc").HasConversion(utcDateTimeOffsetConverter);

            entity.HasIndex(folder => new { folder.DataSourceId, folder.Path }).HasDatabaseName("idx_mail_folders_data_source_path").IsUnique();

            entity
                .HasOne<EmbeddingStateDataSourceEntity>()
                .WithMany()
                .HasForeignKey(folder => folder.DataSourceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasMany(folder => folder.Locations)
                .WithOne(location => location.Folder)
                .HasForeignKey(location => location.FolderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MailLocationEntity>(entity =>
        {
            entity.ToTable("mail_locations");
            entity.HasKey(location => location.Id);

            entity.Property(location => location.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(location => location.ParentFileId).HasColumnName("parent_file_id").IsRequired();
            entity.Property(location => location.FolderId).HasColumnName("folder_id");
            entity.Property(location => location.Uid).HasColumnName("uid");
            entity.Property(location => location.IsSeen).HasColumnName("is_seen");
            entity.Property(location => location.IsFlagged).HasColumnName("is_flagged");
            entity.Property(location => location.IsAnswered).HasColumnName("is_answered");

            entity.HasIndex(location => new { location.FolderId, location.Uid }).HasDatabaseName("idx_mail_locations_folder_uid").IsUnique();
            entity.HasIndex(location => location.ParentFileId).HasDatabaseName("idx_mail_locations_parent_file");
        });

        modelBuilder.Entity<MailboxSyncStateEntity>(entity =>
        {
            entity.ToTable("mailbox_sync_state");
            entity.HasKey(state => state.DataSourceId);

            entity.Property(state => state.DataSourceId).HasColumnName("data_source_id");
            entity.Property(state => state.LastSyncCompletedUtc).HasColumnName("last_sync_completed_utc").HasConversion(utcDateTimeOffsetConverter);
            entity.Property(state => state.PendingRemovalCount).HasColumnName("pending_removal_count");
            entity.Property(state => state.PendingRemovalApprovedUtc).HasColumnName("pending_removal_approved_utc").HasConversion(utcDateTimeOffsetConverter);

            entity
                .HasOne<EmbeddingStateDataSourceEntity>()
                .WithOne()
                .HasForeignKey<MailboxSyncStateEntity>(state => state.DataSourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        //
        // Without any relationship on purpose, cf. MailboxAuthStateEntity: a refused sign-in has
        // to outlive a rebuild of the index.
        //
        modelBuilder.Entity<MailboxAuthStateEntity>(entity =>
        {
            entity.ToTable("mailbox_auth_state");
            entity.HasKey(state => state.DataSourceId);

            entity.Property(state => state.DataSourceId).HasColumnName("data_source_id");
            entity.Property(state => state.FailedAtUtc).HasColumnName("failed_at_utc").HasConversion(utcDateTimeOffsetConverter).IsRequired();
            entity.Property(state => state.FailureMessage).HasColumnName("failure_message").IsRequired();
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
