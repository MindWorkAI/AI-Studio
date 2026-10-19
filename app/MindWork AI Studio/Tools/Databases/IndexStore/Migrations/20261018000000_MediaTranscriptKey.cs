#nullable disable

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AIStudio.Tools.Databases.IndexStore.Migrations;

/// <summary>
/// Adds the key of the transcript to the indexed files, for audio and video files which a data source transcribed.
/// </summary>
/// <remarks>
/// The transcripts lie below the data directory under the hash of the content of their media file.
/// The index has to know which transcript belongs to which file, so that AI Studio can remove a
/// transcript once its file is gone or changed. Every other file keeps NULL.
/// </remarks>
[DbContext(typeof(IndexStoreDbContext))]
[Migration("20261018000000_MediaTranscriptKey")]
public partial class MediaTranscriptKey : Migration
{
    /// <remarks>
    /// Both directions go through raw SQL, like DropDataSourceName: the SQLite provider answers
    /// DropColumn by rebuilding the table, and dropping the old embedded_files table would let the
    /// cascade of the foreign key in embedding_chunks take every chunk with it. Adding a nullable
    /// column is native in SQLite and leaves the rows alone.
    /// </remarks>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE embedded_files ADD COLUMN transcript_key TEXT NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE embedded_files DROP COLUMN transcript_key;");
    }
}