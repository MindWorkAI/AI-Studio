#nullable disable

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AIStudio.Tools.Databases.IndexStore.Migrations;

/// <summary>
/// Adds what a chunk and its file know beyond their text: where a chunk is, the hash of its text,
/// its generation, and the hash of the content of its file.
/// </summary>
/// <remarks>
/// Each chunk gains:
/// <list type="bullet">
///   <item>the page it ends on, next to the page it starts on, and its sheet and rows, its lines, and
///   the attachment of a mail it is from, cf. SourceLocation;</item>
///   <item>the hash of its text, so a document read again keeps the vector of every chunk whose text
///   did not change, cf. ChunkTextHash;</item>
///   <item>its generation, so a document read again gets its new chunks next to the old ones and
///   keeps the old ones until the new ones are complete, cf. IndexedDocumentIds.CreateChunkIdSource.
///   Two generations may hold the same position, so the unique index over parent and position
///   gains the generation as well.</item>
/// </list>
/// Each file gains the hash of its content, so a file saved again without changes needs neither
/// extraction nor embedding, and the generation it is searched by.<br/><br/>
/// Chunks and files stored before keep NULL, which reads as unknown, and generation 0, which is
/// the generation every one of them has: an index built before has one generation per document.
/// They gain the rest once their document is indexed again, so no index has to be rebuilt, cf.
/// DataSourceEmbeddingService.CHUNK_METADATA_VERSION.
/// </remarks>
[DbContext(typeof(IndexStoreDbContext))]
[Migration("20261022000000_ChunkMetadata")]
public partial class ChunkMetadata : Migration
{
    /// <remarks>
    /// Both directions go through raw SQL, like MediaTranscriptKey: the SQLite provider answers
    /// DropColumn by rebuilding the table, which would drop the triggers that keep the full-text
    /// index of the chunks in step, and dropping the old embedded_files table would let the cascade
    /// of the foreign key in embedding_chunks take every chunk with it. Adding a column and
    /// replacing an index are native in SQLite and leave the rows alone.
    /// </remarks>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE embedding_chunks ADD COLUMN end_page_number INTEGER NULL;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks ADD COLUMN sheet_name TEXT NULL;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks ADD COLUMN start_row INTEGER NULL;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks ADD COLUMN end_row INTEGER NULL;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks ADD COLUMN start_line INTEGER NULL;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks ADD COLUMN end_line INTEGER NULL;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks ADD COLUMN attachment_position INTEGER NULL;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks ADD COLUMN text_hash TEXT NULL;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks ADD COLUMN generation INTEGER NOT NULL DEFAULT 0;");

        migrationBuilder.Sql("ALTER TABLE embedded_files ADD COLUMN content_hash TEXT NULL;");
        migrationBuilder.Sql("ALTER TABLE embedded_files ADD COLUMN generation INTEGER NOT NULL DEFAULT 0;");

        migrationBuilder.Sql("DROP INDEX IF EXISTS idx_embedding_chunks_parent_file_chunk_index;");
        migrationBuilder.Sql("CREATE UNIQUE INDEX idx_embedding_chunks_parent_file_generation_chunk_index ON embedding_chunks (parent_file_id, generation, chunk_index);");
    }

    /// <remarks>
    /// Going back needs every document to have a single generation again, as it has whenever no
    /// indexing run is under way and no failed attempt is kept.
    /// </remarks>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS idx_embedding_chunks_parent_file_generation_chunk_index;");
        migrationBuilder.Sql("CREATE UNIQUE INDEX idx_embedding_chunks_parent_file_chunk_index ON embedding_chunks (parent_file_id, chunk_index);");

        migrationBuilder.Sql("ALTER TABLE embedded_files DROP COLUMN generation;");
        migrationBuilder.Sql("ALTER TABLE embedded_files DROP COLUMN content_hash;");

        migrationBuilder.Sql("ALTER TABLE embedding_chunks DROP COLUMN generation;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks DROP COLUMN text_hash;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks DROP COLUMN attachment_position;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks DROP COLUMN end_line;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks DROP COLUMN start_line;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks DROP COLUMN end_row;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks DROP COLUMN start_row;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks DROP COLUMN sheet_name;");
        migrationBuilder.Sql("ALTER TABLE embedding_chunks DROP COLUMN end_page_number;");
    }
}