using AIStudio.Tools.Databases.IndexStore;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AIStudio.Tools.Databases.ProvenanceStore;

internal sealed class ProvenanceStoreDbContext(DbContextOptions<ProvenanceStoreDbContext> options) : DbContext(options)
{
    public static DbContextOptions<ProvenanceStoreDbContext> CreateOptions(string databasePath) => new DbContextOptionsBuilder<ProvenanceStoreDbContext>()
        .UseSqlite(BuildConnectionString(databasePath))
        .Options;

    public DbSet<ProvenanceEntity> Entries => this.Set<ProvenanceEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProvenanceEntity>(entity =>
        {
            entity.ToTable("provenance");
            entity.HasKey(entry => new { entry.ChatId, entry.Id });

            entity.Property(entry => entry.ChatId).HasColumnName("chat_id");
            entity.Property(entry => entry.Id).HasColumnName("id");
            entity.Property(entry => entry.Kind).HasColumnName("kind").IsRequired();
            entity.Property(entry => entry.Content).HasColumnName("content").IsRequired();
            entity.Property(entry => entry.SurroundingContent).HasColumnName("surrounding_content");
            entity.Property(entry => entry.CreatedAtUtc).HasColumnName("created_at_utc").HasConversion(new IndexStoreDateTimeOffsetConverter()).IsRequired();

            // A chat reads its entries by their ids alone, see ProvenanceStoreClient.GetEntriesAsync:
            entity.HasIndex(entry => entry.Id).HasDatabaseName("idx_provenance_id");
        });
    }

    private static string BuildConnectionString(string databasePath) => new SqliteConnectionStringBuilder
    {
        DataSource = databasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Shared,
        DefaultTimeout = 30,
    }.ToString();
}