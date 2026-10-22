#nullable disable

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AIStudio.Tools.Databases.IndexStore.Migrations;

/// <summary>
/// Drops the copy of the data source confidence level which every indexed file carried.
/// </summary>
/// <remarks>
/// The confidence level is what a data source asks of a provider. It is a property of the data
/// source, it is enforced live before anything is indexed or answered, and it changes no vector.
/// Keeping a copy per file only meant the index had to be thrown away whenever the setting changed.
/// </remarks>
[DbContext(typeof(IndexStoreDbContext))]
[Migration("20260915000000_DropFileConfidenceLevel")]
public partial class DropFileConfidenceLevel : Migration
{
    /// <remarks>
    /// The columns go through raw SQL instead of DropColumn on purpose. The SQLite provider answers
    /// DropColumn by rebuilding the table, and a rebuild drops the table the trigger
    /// embedded_files_file_name_au hangs on, which would silently stop the full-text index from
    /// following a renamed file. A native ALTER TABLE ... DROP COLUMN leaves the table itself alone.
    /// It does refuse a column an index names, so the index has to go first.
    /// </remarks>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "idx_embedded_files_confidence",
            table: "embedded_files");

        migrationBuilder.Sql("""
                             ALTER TABLE embedded_files DROP COLUMN confidence_level;
                             ALTER TABLE embedded_files DROP COLUMN confidence_level_rank;
                             """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
                             ALTER TABLE embedded_files ADD COLUMN confidence_level TEXT NOT NULL DEFAULT '';
                             ALTER TABLE embedded_files ADD COLUMN confidence_level_rank INTEGER NOT NULL DEFAULT 0;
                             """);

        migrationBuilder.CreateIndex(
            name: "idx_embedded_files_confidence",
            table: "embedded_files",
            column: "confidence_level_rank");
    }
}