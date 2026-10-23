#nullable disable

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AIStudio.Tools.Databases.IndexStore.Migrations;

/// <summary>
/// Adds its generation to every chunk.
/// </summary>
/// <remarks>
/// A document indexed again gets its new chunks under the next generation, so they never
/// overwrite the old ones, cf. IndexedDocumentIds.CreateChunkIdSource. Chunks stored before are
/// generation 0, the generation of a first indexing, so no index has to be rebuilt.
/// </remarks>
[DbContext(typeof(IndexStoreDbContext))]
[Migration("20261023000001_ChunkGeneration")]
public partial class ChunkGeneration : Migration
{
    /// <remarks>
    /// Both directions go through raw SQL, like ChunkEndPageNumber: the SQLite provider answers
    /// DropColumn by rebuilding the table, which would drop the triggers that keep the full-text
    /// index of the chunks in step. Adding a column with a default is native in SQLite and leaves
    /// the rows alone.
    /// </remarks>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE embedding_chunks ADD COLUMN generation INTEGER NOT NULL DEFAULT 0;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE embedding_chunks DROP COLUMN generation;");
    }
}