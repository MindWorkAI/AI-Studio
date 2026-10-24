using Microsoft.Data.Sqlite;

namespace AIStudio.Tools.Databases;

/// <summary>
/// Copies a SQLite database while AI Studio keeps using it, e.g., for a backup.
/// </summary>
/// <remarks>
/// VACUUM INTO writes a consistent snapshot of the database, including what the write-ahead log
/// still holds, and in WAL mode, reading it holds back no writer.<br/><br/>
///
/// It runs on connections of their own, although the databases guide has raw SQL go through
/// GetDbConnection. The connections of a context share one cache, and a shared cache locks whole
/// tables: reading the whole database for the copy would hold back every writer until the copy is
/// done. Without pooling, nothing keeps the copy open afterward, so the caller can delete it on
/// every OS.<br/><br/>
///
/// SQLite runs its async methods synchronously, and copying and checking a large database takes a
/// while. The callers therefore run this on a thread of their own, so that it never holds up theirs.
/// </remarks>
internal static class SqliteDatabaseCopy
{
    /// <summary>
    /// Copies a database into a file and checks the copy.
    /// </summary>
    /// <param name="databasePath">The database to copy.</param>
    /// <param name="targetPath">The file of the copy. A file there is replaced.</param>
    /// <param name="token">The cancellation token.</param>
    /// <exception cref="InvalidOperationException">The copy failed its check. It is removed then.</exception>
    public static async Task CopyAndCheckAsync(string databasePath, string targetPath, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        // VACUUM INTO refuses to write into a file which holds anything:
        if (File.Exists(targetPath))
            File.Delete(targetPath);

        try
        {
            await using (var connection = new SqliteConnection(BuildConnectionString(databasePath, SqliteOpenMode.ReadWrite)))
            {
                await connection.OpenAsync(token);
                await using var command = connection.CreateCommand();
                command.CommandText = "VACUUM INTO $targetPath;";
                command.Parameters.AddWithValue("$targetPath", targetPath);
                await command.ExecuteNonQueryAsync(token);
            }

            await using (var connection = new SqliteConnection(BuildConnectionString(targetPath, SqliteOpenMode.ReadOnly)))
            {
                await connection.OpenAsync(token);
                await using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA quick_check;";

                var checkResult = (await command.ExecuteScalarAsync(token))?.ToString();
                if (checkResult is not "ok")
                    throw new InvalidOperationException($"The copy of the database at '{targetPath}' failed its check: '{checkResult}'.");
            }
        }
        catch
        {
            if (File.Exists(targetPath))
                File.Delete(targetPath);

            throw;
        }
    }

    /// <summary>
    /// Builds the connection string for a connection of its own, which shares no cache and stays in no pool.
    /// </summary>
    /// <param name="path">The database.</param>
    /// <param name="mode">How to open it.</param>
    /// <returns>The connection string.</returns>
    public static string BuildConnectionString(string path, SqliteOpenMode mode) => new SqliteConnectionStringBuilder
    {
        DataSource = path,
        Mode = mode,
        Cache = SqliteCacheMode.Private,
        Pooling = false,
        DefaultTimeout = 30,
    }.ToString();
}