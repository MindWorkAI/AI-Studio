#nullable disable

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AIStudio.Tools.Databases.IndexStore.Migrations;

/// <summary>
/// Adds what the index keeps about mailboxes beyond the text of their mails.
/// </summary>
/// <remarks>
/// Every mail is a row in embedded_files like any document, so search, chunks and full-text index
/// stay the same for both. These tables hold what only a mail has: its addresses, its parts, the
/// folders and places it lies in, and how far the sync of each mailbox got. All of them go with
/// their data source, except mailbox_auth_state: a refused sign-in has to survive a rebuild.
/// </remarks>
[DbContext(typeof(IndexStoreDbContext))]
[Migration("20261001000000_MailboxIndex")]
public partial class MailboxIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "mail_messages",
            columns: table => new
            {
                parent_file_id = table.Column<string>(type: "TEXT", nullable: false),
                data_source_id = table.Column<string>(type: "TEXT", nullable: false),
                message_id = table.Column<string>(type: "TEXT", nullable: false),
                in_reply_to = table.Column<string>(type: "TEXT", nullable: false),
                reference_message_ids = table.Column<string>(type: "TEXT", nullable: false),
                sent_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                received_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                importance = table.Column<string>(type: "TEXT", nullable: false),
                encryption_kind = table.Column<string>(type: "TEXT", nullable: false),
                mail_hash = table.Column<string>(type: "TEXT", nullable: false),
                first_seen_utc = table.Column<string>(type: "TEXT", nullable: false),
                orphaned_at_utc = table.Column<string>(type: "TEXT", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_mail_messages", mail => mail.parent_file_id);
                table.ForeignKey(
                    name: "FK_mail_messages_data_sources_data_source_id",
                    column: mail => mail.data_source_id,
                    principalTable: "data_sources",
                    principalColumn: "data_source_id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_mail_messages_embedded_files_parent_file_id",
                    column: mail => mail.parent_file_id,
                    principalTable: "embedded_files",
                    principalColumn: "parent_file_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "mail_folders",
            columns: table => new
            {
                id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                data_source_id = table.Column<string>(type: "TEXT", nullable: false),
                path = table.Column<string>(type: "TEXT", nullable: false),
                special_use = table.Column<string>(type: "TEXT", nullable: false),
                uid_validity = table.Column<long>(type: "INTEGER", nullable: false),
                uid_next = table.Column<long>(type: "INTEGER", nullable: true),
                highest_mod_seq = table.Column<long>(type: "INTEGER", nullable: true),
                server_message_count = table.Column<long>(type: "INTEGER", nullable: true),
                server_unseen_count = table.Column<long>(type: "INTEGER", nullable: true),
                initial_sync_completed_utc = table.Column<string>(type: "TEXT", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_mail_folders", folder => folder.id);
                table.ForeignKey(
                    name: "FK_mail_folders_data_sources_data_source_id",
                    column: folder => folder.data_source_id,
                    principalTable: "data_sources",
                    principalColumn: "data_source_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "mail_addresses",
            columns: table => new
            {
                id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                parent_file_id = table.Column<string>(type: "TEXT", nullable: false),
                role = table.Column<string>(type: "TEXT", nullable: false),
                position = table.Column<int>(type: "INTEGER", nullable: false),
                address = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                display_name = table.Column<string>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_mail_addresses", address => address.id);
                table.ForeignKey(
                    name: "FK_mail_addresses_mail_messages_parent_file_id",
                    column: address => address.parent_file_id,
                    principalTable: "mail_messages",
                    principalColumn: "parent_file_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "mail_parts",
            columns: table => new
            {
                id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                parent_file_id = table.Column<string>(type: "TEXT", nullable: false),
                kind = table.Column<string>(type: "TEXT", nullable: false),
                position = table.Column<int>(type: "INTEGER", nullable: false),
                name = table.Column<string>(type: "TEXT", nullable: false),
                content_type = table.Column<string>(type: "TEXT", nullable: false),
                part_size = table.Column<long>(type: "INTEGER", nullable: false),
                text = table.Column<string>(type: "TEXT", nullable: true),
                text_state = table.Column<string>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_mail_parts", part => part.id);
                table.ForeignKey(
                    name: "FK_mail_parts_mail_messages_parent_file_id",
                    column: part => part.parent_file_id,
                    principalTable: "mail_messages",
                    principalColumn: "parent_file_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "mail_locations",
            columns: table => new
            {
                id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                parent_file_id = table.Column<string>(type: "TEXT", nullable: false),
                folder_id = table.Column<int>(type: "INTEGER", nullable: false),
                uid = table.Column<long>(type: "INTEGER", nullable: false),
                is_seen = table.Column<bool>(type: "INTEGER", nullable: false),
                is_flagged = table.Column<bool>(type: "INTEGER", nullable: false),
                is_answered = table.Column<bool>(type: "INTEGER", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_mail_locations", location => location.id);
                table.ForeignKey(
                    name: "FK_mail_locations_mail_folders_folder_id",
                    column: location => location.folder_id,
                    principalTable: "mail_folders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_mail_locations_mail_messages_parent_file_id",
                    column: location => location.parent_file_id,
                    principalTable: "mail_messages",
                    principalColumn: "parent_file_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "mailbox_sync_state",
            columns: table => new
            {
                data_source_id = table.Column<string>(type: "TEXT", nullable: false),
                last_sync_completed_utc = table.Column<string>(type: "TEXT", nullable: true),
                pending_removal_count = table.Column<int>(type: "INTEGER", nullable: true),
                pending_removal_approved_utc = table.Column<string>(type: "TEXT", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_mailbox_sync_state", state => state.data_source_id);
                table.ForeignKey(
                    name: "FK_mailbox_sync_state_data_sources_data_source_id",
                    column: state => state.data_source_id,
                    principalTable: "data_sources",
                    principalColumn: "data_source_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "mailbox_auth_state",
            columns: table => new
            {
                data_source_id = table.Column<string>(type: "TEXT", nullable: false),
                failed_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                failure_message = table.Column<string>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_mailbox_auth_state", state => state.data_source_id);
            });

        migrationBuilder.CreateIndex(
            name: "idx_mail_messages_data_source_message_id",
            table: "mail_messages",
            columns: ["data_source_id", "message_id"]);

        migrationBuilder.CreateIndex(
            name: "idx_mail_messages_data_source_received",
            table: "mail_messages",
            columns: ["data_source_id", "received_at_utc"]);

        migrationBuilder.CreateIndex(
            name: "idx_mail_folders_data_source_path",
            table: "mail_folders",
            columns: ["data_source_id", "path"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "idx_mail_addresses_address",
            table: "mail_addresses",
            column: "address");

        migrationBuilder.CreateIndex(
            name: "idx_mail_addresses_parent_file_role_position",
            table: "mail_addresses",
            columns: ["parent_file_id", "role", "position"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "idx_mail_parts_parent_file_kind_position",
            table: "mail_parts",
            columns: ["parent_file_id", "kind", "position"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "idx_mail_locations_folder_uid",
            table: "mail_locations",
            columns: ["folder_id", "uid"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "idx_mail_locations_parent_file",
            table: "mail_locations",
            column: "parent_file_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "mailbox_auth_state");
        migrationBuilder.DropTable(name: "mailbox_sync_state");
        migrationBuilder.DropTable(name: "mail_locations");
        migrationBuilder.DropTable(name: "mail_parts");
        migrationBuilder.DropTable(name: "mail_addresses");
        migrationBuilder.DropTable(name: "mail_folders");
        migrationBuilder.DropTable(name: "mail_messages");
    }
}