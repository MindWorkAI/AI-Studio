#nullable disable

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AIStudio.Tools.Databases.IndexStore.Migrations;

/// <summary>
/// Lets two generations of the chunks of a document exist side by side, and records which one the document is searched by.
/// </summary>
/// <remarks>
/// A document read again keeps its stored chunks until all new ones are stored, and keeps them for
/// good when the attempt fails, cf. EmbeddingStateFileEntity.Generation. Each file starts with the
/// highest generation of its chunks, which is 0 for every file indexed before generations existed.
/// </remarks>
[DbContext(typeof(IndexStoreDbContext))]
[Migration("20261023000003_IndexedFileGeneration")]
public partial class IndexedFileGeneration : Migration
{
    /// <remarks>
    /// Raw SQL throughout, like MediaTranscriptKey: dropping a column makes the SQLite provider
    /// rebuild the table, which would cascade into the chunks. Adding a column and replacing an
    /// index leave the rows and the triggers of the full-text index alone.
    /// </remarks>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE embedded_files ADD COLUMN generation INTEGER NOT NULL DEFAULT 0;");
        migrationBuilder.Sql("""
                             UPDATE embedded_files
                             SET generation = COALESCE((SELECT MAX(chunk.generation) FROM embedding_chunks chunk WHERE chunk.parent_file_id = embedded_files.parent_file_id), 0);
                             """);

        migrationBuilder.Sql("DROP INDEX IF EXISTS idx_embedding_chunks_parent_file_chunk_index;");
        migrationBuilder.Sql("CREATE UNIQUE INDEX idx_embedding_chunks_parent_file_generation_chunk_index ON embedding_chunks (parent_file_id, generation, chunk_index);");
    }

    /// <remarks>
    /// Going back needs every document to have a single generation again, as it has whenever no
    /// indexing run is under way.
    /// </remarks>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS idx_embedding_chunks_parent_file_generation_chunk_index;");
        migrationBuilder.Sql("CREATE UNIQUE INDEX idx_embedding_chunks_parent_file_chunk_index ON embedding_chunks (parent_file_id, chunk_index);");
        migrationBuilder.Sql("ALTER TABLE embedded_files DROP COLUMN generation;");
    }
}