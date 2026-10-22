#nullable disable

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AIStudio.Tools.Databases.IndexStore.Migrations;

/// <summary>
/// Adds the page a chunk ends on to the chunks, next to the page it starts on.
/// </summary>
/// <remarks>
/// A chunk may span several pages, and the user is shown which. Chunks stored before keep NULL,
/// which reads as "not known": they name their start page until their file is indexed again, so
/// no index has to be rebuilt for this, cf. DataSourceEmbeddingService.CHUNK_METADATA_VERSION.
/// </remarks>
[DbContext(typeof(IndexStoreDbContext))]
[Migration("20261022000000_ChunkEndPageNumber")]
public partial class ChunkEndPageNumber : Migration
{
    /// <remarks>
    /// Both directions go through raw SQL, like MediaTranscriptKey: the SQLite provider answers
    /// DropColumn by rebuilding the table, which would drop the triggers that keep the full-text
    /// index of the chunks in step. Adding a nullable column is native in SQLite and leaves the
    /// rows alone.
    /// </remarks>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE embedding_chunks ADD COLUMN end_page_number INTEGER NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE embedding_chunks DROP COLUMN end_page_number;");
    }
}