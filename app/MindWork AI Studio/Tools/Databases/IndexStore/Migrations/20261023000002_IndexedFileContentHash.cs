#nullable disable

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AIStudio.Tools.Databases.IndexStore.Migrations;

/// <summary>
/// Adds the hash of the content of the file to the indexed files.
/// </summary>
/// <remarks>
/// When the fingerprint of a file changes but its content does not, e.g., after it was saved again
/// or synchronized, the file needs neither extraction nor embedding. Files indexed before keep NULL
/// and are read once more the next time their fingerprint changes, which stores their hash. Mails
/// keep NULL for good: their fingerprint never changes.
/// </remarks>
[DbContext(typeof(IndexStoreDbContext))]
[Migration("20261023000002_IndexedFileContentHash")]
public partial class IndexedFileContentHash : Migration
{
    /// <remarks>
    /// Both directions go through raw SQL, like MediaTranscriptKey: the SQLite provider answers
    /// DropColumn by rebuilding the table, and dropping the old embedded_files table would let the
    /// cascade of the foreign key in embedding_chunks take every chunk with it. Adding a nullable
    /// column is native in SQLite and leaves the rows alone.
    /// </remarks>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE embedded_files ADD COLUMN content_hash TEXT NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE embedded_files DROP COLUMN content_hash;");
    }
}