using AIStudio.Tools.Databases.IndexStore;

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

    public ValueTask DisposeAsync()
    {
        // The pooled connections hold the file open, which would keep it from being deleted on Windows:
        SqliteConnection.ClearAllPools();
        Directory.Delete(directory, true);
        return ValueTask.CompletedTask;
    }
}