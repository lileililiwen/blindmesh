using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blindmesh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ModerationArbitration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "dispute_reason",
                table: "submissions",
                type: "character varying(5000)",
                maxLength: 5000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "account_bans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    banned_until = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ban_reason = table.Column<string>(type: "text", nullable: true),
                    banned_by = table.Column<Guid>(type: "uuid", nullable: true),
                    banned_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_bans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "disputes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    publisher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contributor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: true),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    base_reward = table.Column<long>(type: "bigint", nullable: false),
                    award_to_contributor = table.Column<bool>(type: "boolean", nullable: true),
                    resolution_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ledger_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ledger_transaction_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    appealed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    appealed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    appeal_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_disputes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "moderation_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispute_id = table.Column<Guid>(type: "uuid", nullable: true),
                    report_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    reason = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_moderation_audit", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reason = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    subject_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: true),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    resolution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dispute_evidence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispute_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    note = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispute_evidence", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispute_evidence_disputes_dispute_id",
                        column: x => x.dispute_id,
                        principalTable: "disputes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "moderator_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispute_id = table.Column<Guid>(type: "uuid", nullable: false),
                    moderator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    released_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_moderator_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_moderator_assignments_disputes_dispute_id",
                        column: x => x.dispute_id,
                        principalTable: "disputes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_account_bans_user_id",
                table: "account_bans",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dispute_evidence_dispute_id_created_at",
                table: "dispute_evidence",
                columns: new[] { "dispute_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_disputes_report_id",
                table: "disputes",
                column: "report_id");

            migrationBuilder.CreateIndex(
                name: "ix_disputes_state_created_at",
                table: "disputes",
                columns: new[] { "state", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_disputes_submission_id_state",
                table: "disputes",
                columns: new[] { "submission_id", "state" });

            migrationBuilder.CreateIndex(
                name: "ix_moderation_audit_dispute_id_created_at",
                table: "moderation_audit",
                columns: new[] { "dispute_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_moderation_audit_report_id_created_at",
                table: "moderation_audit",
                columns: new[] { "report_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_moderator_assignments_dispute_id_assigned_at",
                table: "moderator_assignments",
                columns: new[] { "dispute_id", "assigned_at" });

            migrationBuilder.CreateIndex(
                name: "ix_moderator_assignments_dispute_id_moderator_id",
                table: "moderator_assignments",
                columns: new[] { "dispute_id", "moderator_id" });

            migrationBuilder.CreateIndex(
                name: "ix_reports_reporter_id_created_at",
                table: "reports",
                columns: new[] { "reporter_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reports_state_created_at",
                table: "reports",
                columns: new[] { "state", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_reports_receipt",
                table: "reports",
                column: "receipt",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_bans");

            migrationBuilder.DropTable(
                name: "dispute_evidence");

            migrationBuilder.DropTable(
                name: "moderation_audit");

            migrationBuilder.DropTable(
                name: "moderator_assignments");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "disputes");

            migrationBuilder.AlterColumn<string>(
                name: "dispute_reason",
                table: "submissions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(5000)",
                oldMaxLength: 5000,
                oldNullable: true);
        }
    }
}
