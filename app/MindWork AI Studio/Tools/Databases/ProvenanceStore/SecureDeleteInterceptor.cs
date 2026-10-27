using System.Data.Common;

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AIStudio.Tools.Databases.ProvenanceStore;

/// <summary>
/// Turns on secure_delete for every connection to the provenance store.
/// </summary>
/// <remarks>
/// SQLite only marks the space of a deleted row as free, and its bytes stay in the file until
/// another row takes their place. The provenance store holds whole documents, mails, and web pages,
/// so the text of a deleted chat would stay readable in the file, perhaps for months. With
/// secure_delete, SQLite overwrites it with zeros while deleting. The write-ahead log keeps the old
/// pages until its next checkpoint, which SQLite makes by itself.<br/><br/>
///
/// The setting lasts as long as its connection, hence it is made for every connection anew.
/// </remarks>
internal sealed class SecureDeleteInterceptor : DbConnectionInterceptor
{
    private const string PRAGMA = "PRAGMA secure_delete = ON;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = connection.CreateCommand();
        command.CommandText = PRAGMA;
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = PRAGMA;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}