#nullable disable

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AIStudio.Tools.Databases.IndexStore.Migrations;

/// <summary>
/// Drops the copy of the data source name which the index kept next to each indexed data source.
/// </summary>
/// <remarks>
/// The name a user gives a data source lives in the configuration and is read from there whenever
/// it is needed. The copy here was only ever written, never read, and a copy of a name people are
/// free to change can do nothing but go stale.
/// </remarks>
[DbContext(typeof(IndexStoreDbContext))]
[Migration("20260916000000_DropDataSourceName")]
public partial class DropDataSourceName : Migration
{
    /// <remarks>
    /// The column goes through raw SQL instead of DropColumn on purpose. The SQLite provider answers
    /// DropColumn by rebuilding the table, and dropping the old data_sources table would let the
    /// cascade of the foreign key in embedded_files take every indexed file and chunk with it. A
    /// native ALTER TABLE ... DROP COLUMN leaves the table itself alone. No index names this column,
    /// so nothing has to be dropped first.
    /// </remarks>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE data_sources DROP COLUMN data_source_name;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE data_sources ADD COLUMN data_source_name TEXT NOT NULL DEFAULT '';");
    }
}