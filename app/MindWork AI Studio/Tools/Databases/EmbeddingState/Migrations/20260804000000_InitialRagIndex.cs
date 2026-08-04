#nullable disable

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AIStudio.Tools.Databases.EmbeddingState.Migrations;

[DbContext(typeof(EmbeddingStateDbContext))]
[Migration("20260804000000_InitialRagIndex")]
public partial class InitialRagIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "data_sources",
            columns: table => new
            {
                data_source_id = table.Column<string>(type: "TEXT", nullable: false),
                data_source_name = table.Column<string>(type: "TEXT", nullable: false),
                data_source_type = table.Column<string>(type: "TEXT", nullable: false),
                embedding_provider_id = table.Column<string>(type: "TEXT", nullable: false),
                embedding_signature = table.Column<string>(type: "TEXT", nullable: false),
                source_hash = table.Column<string>(type: "TEXT", nullable: false, defaultValue: string.Empty),
                vector_size = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                updated_at_utc = table.Column<string>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_data_sources", source => source.data_source_id);
            });

        migrationBuilder.CreateTable(
            name: "embedded_files",
            columns: table => new
            {
                parent_file_id = table.Column<string>(type: "TEXT", nullable: false),
                data_source_id = table.Column<string>(type: "TEXT", nullable: false),
                absolute_path = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                file_name = table.Column<string>(type: "TEXT", nullable: false),
                relative_path = table.Column<string>(type: "TEXT", nullable: false),
                file_type = table.Column<string>(type: "TEXT", nullable: false),
                fingerprint = table.Column<string>(type: "TEXT", nullable: false),
                file_size = table.Column<long>(type: "INTEGER", nullable: false),
                creation_utc = table.Column<string>(type: "TEXT", nullable: false),
                last_write_utc = table.Column<string>(type: "TEXT", nullable: false),
                embedded_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                chunk_count = table.Column<int>(type: "INTEGER", nullable: false),
                compliance_level = table.Column<string>(type: "TEXT", nullable: false),
                compliance_level_rank = table.Column<int>(type: "INTEGER", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_embedded_files", file => file.parent_file_id);
                table.ForeignKey(
                    name: "FK_embedded_files_data_sources_data_source_id",
                    column: file => file.data_source_id,
                    principalTable: "data_sources",
                    principalColumn: "data_source_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "embedding_chunks",
            columns: table => new
            {
                id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                chunk_id = table.Column<string>(type: "TEXT", nullable: false),
                parent_file_id = table.Column<string>(type: "TEXT", nullable: false),
                page_number = table.Column<int>(type: "INTEGER", nullable: true),
                chunk_index = table.Column<int>(type: "INTEGER", nullable: false),
                chunk_text = table.Column<string>(type: "TEXT", nullable: false),
                embedded_at_utc = table.Column<string>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_embedding_chunks", chunk => chunk.id);
                table.ForeignKey(
                    name: "FK_embedding_chunks_embedded_files_parent_file_id",
                    column: chunk => chunk.parent_file_id,
                    principalTable: "embedded_files",
                    principalColumn: "parent_file_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_embedded_files_absolute_path",
            table: "embedded_files",
            column: "absolute_path");

        migrationBuilder.CreateIndex(
            name: "idx_embedded_files_compliance",
            table: "embedded_files",
            column: "compliance_level_rank");

        migrationBuilder.CreateIndex(
            name: "idx_embedded_files_data_source",
            table: "embedded_files",
            column: "data_source_id");

        migrationBuilder.CreateIndex(
            name: "idx_embedded_files_data_source_absolute_path",
            table: "embedded_files",
            columns: ["data_source_id", "absolute_path"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "idx_embedded_files_file_type",
            table: "embedded_files",
            column: "file_type");

        migrationBuilder.CreateIndex(
            name: "idx_embedding_chunks_chunk_id",
            table: "embedding_chunks",
            column: "chunk_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "idx_embedding_chunks_page",
            table: "embedding_chunks",
            column: "page_number");

        migrationBuilder.CreateIndex(
            name: "idx_embedding_chunks_parent_file",
            table: "embedding_chunks",
            column: "parent_file_id");

        migrationBuilder.CreateIndex(
            name: "idx_embedding_chunks_parent_file_chunk_index",
            table: "embedding_chunks",
            columns: ["parent_file_id", "chunk_index"],
            unique: true);

        migrationBuilder.Sql("""
                             CREATE VIRTUAL TABLE IF NOT EXISTS embedding_chunks_fts
                                 USING fts5(chunk_id UNINDEXED, file_name, chunk_text);

                             CREATE TRIGGER IF NOT EXISTS embedding_chunks_ai
                                 AFTER INSERT ON embedding_chunks
                             BEGIN
                                 INSERT INTO embedding_chunks_fts(rowid, chunk_id, file_name, chunk_text)
                                 VALUES (
                                     new.id,
                                     new.chunk_id,
                                     (SELECT file_name FROM embedded_files WHERE parent_file_id = new.parent_file_id),
                                     new.chunk_text);
                             END;

                             CREATE TRIGGER IF NOT EXISTS embedding_chunks_ad
                                 AFTER DELETE ON embedding_chunks
                             BEGIN
                                 DELETE FROM embedding_chunks_fts
                                 WHERE rowid = old.id;
                             END;

                             CREATE TRIGGER IF NOT EXISTS embedding_chunks_au
                                 AFTER UPDATE ON embedding_chunks
                             BEGIN
                                 DELETE FROM embedding_chunks_fts
                                 WHERE rowid = old.id;

                                 INSERT INTO embedding_chunks_fts(rowid, chunk_id, file_name, chunk_text)
                                 VALUES (
                                     new.id,
                                     new.chunk_id,
                                     (SELECT file_name FROM embedded_files WHERE parent_file_id = new.parent_file_id),
                                     new.chunk_text);
                             END;

                             CREATE TRIGGER IF NOT EXISTS embedded_files_file_name_au
                                 AFTER UPDATE OF file_name ON embedded_files
                             BEGIN
                                 DELETE FROM embedding_chunks_fts
                                 WHERE rowid IN (
                                     SELECT id
                                     FROM embedding_chunks
                                     WHERE parent_file_id = new.parent_file_id
                                 );

                                 INSERT INTO embedding_chunks_fts(rowid, chunk_id, file_name, chunk_text)
                                 SELECT id, chunk_id, new.file_name, chunk_text
                                 FROM embedding_chunks
                                 WHERE parent_file_id = new.parent_file_id;
                             END;
                             """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
                             DROP TRIGGER IF EXISTS embedded_files_file_name_au;
                             DROP TRIGGER IF EXISTS embedding_chunks_au;
                             DROP TRIGGER IF EXISTS embedding_chunks_ad;
                             DROP TRIGGER IF EXISTS embedding_chunks_ai;
                             DROP TABLE IF EXISTS embedding_chunks_fts;
                             """);

        migrationBuilder.DropTable(name: "embedding_chunks");
        migrationBuilder.DropTable(name: "embedded_files");
        migrationBuilder.DropTable(name: "data_sources");
    }
}
