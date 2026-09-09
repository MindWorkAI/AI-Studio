#nullable disable

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AIStudio.Tools.Databases.IndexStore.Migrations;

[DbContext(typeof(IndexStoreDbContext))]
[Migration("20260909000000_PermanentIndexingFailures")]
public partial class PermanentIndexingFailures : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "permanent_indexing_failures",
            columns: table => new
            {
                parent_file_id = table.Column<string>(type: "TEXT", nullable: false),
                data_source_id = table.Column<string>(type: "TEXT", nullable: false),
                absolute_path = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                fingerprint = table.Column<string>(type: "TEXT", nullable: false),
                failure_code = table.Column<string>(type: "TEXT", nullable: false),
                failure_message = table.Column<string>(type: "TEXT", nullable: false),
                occurred_at_utc = table.Column<string>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_permanent_indexing_failures", failure => failure.parent_file_id);
                table.ForeignKey(
                    name: "FK_permanent_indexing_failures_data_sources_data_source_id",
                    column: failure => failure.data_source_id,
                    principalTable: "data_sources",
                    principalColumn: "data_source_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_permanent_indexing_failures_data_source",
            table: "permanent_indexing_failures",
            column: "data_source_id");

        migrationBuilder.CreateIndex(
            name: "idx_permanent_indexing_failures_data_source_absolute_path",
            table: "permanent_indexing_failures",
            columns: ["data_source_id", "absolute_path"],
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "permanent_indexing_failures");
    }
}