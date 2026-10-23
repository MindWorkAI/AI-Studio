#nullable disable

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AIStudio.Tools.Databases.IndexStore.Migrations;

/// <summary>
/// Adds the hash of its text to every chunk.
/// </summary>
/// <remarks>
/// A document read again keeps the vector of every chunk whose text did not change, cf.
/// ChunkTextHash. Chunks stored before keep NULL and are hashed from their text when needed, so
/// nothing has to be filled in and no index has to be rebuilt.
/// </remarks>
[DbContext(typeof(IndexStoreDbContext))]
[Migration("20261023000000_ChunkTextHash")]
public partial class ChunkTextHash : Migration
{
    /// <remarks>
    /// Both directions go through raw SQL, like ChunkEndPageNumber: the SQLite provider answers
    /// DropColumn by rebuilding the table, which would drop the triggers that keep the full-text
    /// index of the chunks in step. Adding a nullable column is native in SQLite and leaves the
    /// rows alone.
    /// </remarks>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE embedding_chunks ADD COLUMN text_hash TEXT NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE embedding_chunks DROP COLUMN text_hash;");
    }
}