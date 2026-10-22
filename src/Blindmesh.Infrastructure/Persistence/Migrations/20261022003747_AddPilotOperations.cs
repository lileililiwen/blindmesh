using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blindmesh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPilotOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "operations_backups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    taken_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    checksum_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    retention_until = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operations_backups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "operations_deletion_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    pseudonymized_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    audit_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operations_deletion_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "operations_restore_rehearsals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    backup_id = table.Column<Guid>(type: "uuid", nullable: true),
                    @namespace = table.Column<string>(name: "namespace", type: "character varying(80)", maxLength: 80, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    schema_check = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    data_check = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    object_check = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ledger_check = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    browser_journeys = table.Column<int>(type: "integer", nullable: true),
                    axe_runs = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operations_restore_rehearsals", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_operations_backups_checksum_sha256",
                table: "operations_backups",
                column: "checksum_sha256");

            migrationBuilder.CreateIndex(
                name: "ix_operations_backups_taken_at",
                table: "operations_backups",
                column: "taken_at");

            migrationBuilder.CreateIndex(
                name: "ix_operations_deletion_requests_received_at",
                table: "operations_deletion_requests",
                column: "received_at");

            migrationBuilder.CreateIndex(
                name: "ix_operations_deletion_requests_subject_user_id",
                table: "operations_deletion_requests",
                column: "subject_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_operations_restore_rehearsals_backup_id",
                table: "operations_restore_rehearsals",
                column: "backup_id");

            migrationBuilder.CreateIndex(
                name: "ix_operations_restore_rehearsals_started_at",
                table: "operations_restore_rehearsals",
                column: "started_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operations_backups");

            migrationBuilder.DropTable(
                name: "operations_deletion_requests");

            migrationBuilder.DropTable(
                name: "operations_restore_rehearsals");
        }
    }
}
