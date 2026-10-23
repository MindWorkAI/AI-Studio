using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Services.Indexing;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AIStudio.Tests.Tools.Databases;

/// <summary>
/// An index store in a directory of its own, migrated the way the app migrates its own, and deleted
/// again afterward.
/// </summary>
/// <param name="directory">The directory the database lives in.</param>
/// <param name="databasePath">The path of the database file.</param>
internal sealed class TemporaryIndexStore(string directory, string databasePath) : IAsyncDisposable
{
    public SqliteIndexStoreClientImplementation Client { get; } = new("SQLite", databasePath, directory, string.Empty);

    public static async Task<TemporaryIndexStore> CreateAsync()
    {
        var store = Create();
        await store.MigrateAsync();
        return store;
    }

    /// <summary>
    /// Creates a store migrated only up to the given migration, the way an older version of the app left it.
    /// </summary>
    /// <remarks>
    /// The client maps the current schema, so it may fail on such a store. Write the rows of the
    /// older schema with raw SQL through CreateContext, then call MigrateAsync.
    /// </remarks>
    /// <param name="targetMigration">The id of the last migration to apply, e.g., "20261022000000_ChunkEndPageNumber".</param>
    public static async Task<TemporaryIndexStore> CreateAsync(string targetMigration)
    {
        var store = Create();
        await using var context = store.CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(targetMigration);
        return store;
    }

    /// <summary>
    /// Applies every migration which is still missing, the way the app does when it starts.
    /// </summary>
    public async Task MigrateAsync()
    {
        await using var context = this.CreateContext();
        await IndexStoreSchemaMigrator.MigrateAsync(context, CancellationToken.None);
    }

    private static TemporaryIndexStore Create()
    {
        SQLitePCL.Batteries_V2.Init();

        var directory = Path.Combine(Path.GetTempPath(), $"ai-studio-index-store-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        return new TemporaryIndexStore(directory, Path.Combine(directory, "rag-index.sqlite3"));
    }

    /// <summary>
    /// Opens the database directly, to set up or check what the client does not cover.
    /// </summary>
    public IndexStoreDbContext CreateContext() => new(IndexStoreDbContext.CreateOptions(databasePath));

    /// <summary>
    /// Stores a mail the way the sync does: first its document with a single chunk, then the mail itself.
    /// </summary>
    /// <param name="dataSourceId">The mailbox, which has to be stored together with the folders of the mail.</param>
    /// <param name="name">A name for the mail, unique within the mailbox. It makes up the key of the document and its fingerprint.</param>
    /// <param name="subject">The subject, which the document keeps as its file name.</param>
    /// <param name="text">The text of the single chunk.</param>
    /// <param name="mail">The mail. Its id is replaced by the id of its document.</param>
    /// <returns>The id of the mail.</returns>
    public async Task<string> StoreMailAsync(string dataSourceId, string name, string subject, string text, MailRecord mail)
    {
        var key = $"mail:{name}";
        var mailId = IndexedDocumentIds.CreateParentId(dataSourceId, key);
        var now = DateTimeOffset.UtcNow;

        await this.Client.UpsertFileAsync(dataSourceId, new EmbeddingStateFile(mailId, key, subject, key, "mail", name, 2048, now, now, now, 1), CancellationToken.None);
        await this.Client.UpsertChunksAsync(dataSourceId, [new EmbeddingStateChunk(IndexedDocumentIds.CreateChunkId(dataSourceId, name, 0), mailId, null, null, 0, text, now)], CancellationToken.None);
        await this.Client.UpsertMailAsync(dataSourceId, mail with { MailId = mailId }, CancellationToken.None);
        return mailId;
    }

    public ValueTask DisposeAsync()
    {
        // The pooled connections hold the file open, which would keep it from being deleted on Windows:
        SqliteConnection.ClearAllPools();
        Directory.Delete(directory, true);
        return ValueTask.CompletedTask;
    }
}