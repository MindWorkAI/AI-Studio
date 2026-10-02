using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Services.Indexing;

using Microsoft.Data.Sqlite;

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
        SQLitePCL.Batteries_V2.Init();

        var directory = Path.Combine(Path.GetTempPath(), $"ai-studio-index-store-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        var databasePath = Path.Combine(directory, "rag-index.sqlite3");
        await using (var context = new IndexStoreDbContext(IndexStoreDbContext.CreateOptions(databasePath)))
            await IndexStoreSchemaMigrator.MigrateAsync(context, CancellationToken.None);

        return new TemporaryIndexStore(directory, databasePath);
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
        await this.Client.UpsertChunksAsync(dataSourceId, [new EmbeddingStateChunk(IndexedDocumentIds.CreateChunkId(dataSourceId, name, 0), mailId, null, 0, text, now)], CancellationToken.None);
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