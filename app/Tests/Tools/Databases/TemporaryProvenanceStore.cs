using System.Reflection;

using AIStudio.Tools.Databases;
using AIStudio.Tools.Databases.ProvenanceStore;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.Databases;

/// <summary>
/// A provenance store in a directory of its own, migrated the way the app migrates its own, and
/// deleted again afterward.
/// </summary>
/// <param name="directory">The directory the database lives in.</param>
/// <param name="databasePath">The path of the database file.</param>
internal sealed class TemporaryProvenanceStore(string directory, string databasePath) : IAsyncDisposable
{
    public SqliteProvenanceStoreClientImplementation Client { get; } = new("SQLite", databasePath, directory, string.Empty);

    /// <summary>
    /// The file of the database, to check what it holds on the disk.
    /// </summary>
    public string DatabasePath => databasePath;

    public static async Task<TemporaryProvenanceStore> CreateAsync()
    {
        SQLitePCL.Batteries_V2.Init();

        var directory = Path.Combine(Path.GetTempPath(), $"ai-studio-provenance-store-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        var store = new TemporaryProvenanceStore(directory, Path.Combine(directory, "provenance.sqlite3"));
        await using var context = store.CreateContext();
        await ProvenanceStoreSchemaMigrator.MigrateAsync(context, CancellationToken.None);
        return store;
    }

    /// <summary>
    /// Makes a provider of the database clients which hands out this store, and nothing else.
    /// </summary>
    public DatabaseClientProvider CreateClientProvider()
    {
        var databases = new DatabaseClientProvider(null!, NullLoggerFactory.Instance);
        var clients = (Dictionary<DatabaseRole, DatabaseClient>)typeof(DatabaseClientProvider).GetField("clients", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(databases)!;
        clients.Add(DatabaseRole.PROVENANCE_STORE, this.Client);
        return databases;
    }

    /// <summary>
    /// Opens the database directly, to check what the client does not show.
    /// </summary>
    public ProvenanceStoreDbContext CreateContext() => new(ProvenanceStoreDbContext.CreateOptions(databasePath));

    public ValueTask DisposeAsync()
    {
        // The pooled connections hold the file open, which would keep it from being deleted on Windows:
        SqliteConnection.ClearAllPools();
        Directory.Delete(directory, true);
        return ValueTask.CompletedTask;
    }
}