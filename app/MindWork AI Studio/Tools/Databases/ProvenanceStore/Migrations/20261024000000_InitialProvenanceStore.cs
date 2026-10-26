#nullable disable

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AIStudio.Tools.Databases.ProvenanceStore.Migrations;

/// <summary>
/// Creates the table provenance, which holds what chats received besides their messages.
/// </summary>
[DbContext(typeof(ProvenanceStoreDbContext))]
[Migration("20261024000000_InitialProvenanceStore")]
public partial class InitialProvenanceStore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "provenance",
            columns: table => new
            {
                chat_id = table.Column<string>(type: "TEXT", nullable: false),
                id = table.Column<string>(type: "TEXT", nullable: false),
                kind = table.Column<string>(type: "TEXT", nullable: false),
                content = table.Column<string>(type: "TEXT", nullable: false),
                surrounding_content = table.Column<string>(type: "TEXT", nullable: true),
                created_at_utc = table.Column<string>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_provenance", entry => new { entry.chat_id, entry.id });
            });

        migrationBuilder.CreateIndex(
            name: "idx_provenance_id",
            table: "provenance",
            column: "id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "provenance");
    }
}