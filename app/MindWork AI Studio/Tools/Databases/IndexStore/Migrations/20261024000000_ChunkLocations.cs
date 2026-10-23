#nullable disable

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AIStudio.Tools.Databases.IndexStore.Migrations;

/// <summary>
/// Adds where a chunk is to every chunk: its sheet and rows, its lines, and the attachment of a mail it is from.
/// </summary>
/// <remarks>
/// The runtime states all of these while it reads a document, cf. SourceLocation. Chunks stored
/// before keep NULL, which reads as unknown, and gain them once their document is indexed again.
/// Nothing has to be filled in, and no index has to be rebuilt.
/// </remarks>
[DbContext(typeof(IndexStoreDbContext))]
[Migration("20261024000000_ChunkLocations")]
public partial class ChunkLocations : Migration
{
    /// <remarks>
    /// Both directions go through raw SQL, like ChunkEndPageNumber: the SQLite provider answers
    /// DropColumn by rebuilding the table, which would drop the triggers that keep the full-text
    /// index of the chunks in step. Adding a nullable column is native in SQLite and leaves the
    /// rows alone.
    /// </remarks>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE embedding_chunks ADD COLUMN sheet_name TEXT NULL;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks ADD COLUMN start_row INTEGER NULL;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks ADD COLUMN end_row INTEGER NULL;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks ADD COLUMN start_line INTEGER NULL;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks ADD COLUMN end_line INTEGER NULL;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks ADD COLUMN attachment_position INTEGER NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE embedding_chunks DROP COLUMN attachment_position;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks DROP COLUMN end_line;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks DROP COLUMN start_line;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks DROP COLUMN end_row;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks DROP COLUMN start_row;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks DROP COLUMN sheet_name;");
    }
}