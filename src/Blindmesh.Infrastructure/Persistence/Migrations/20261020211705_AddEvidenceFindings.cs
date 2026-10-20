using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blindmesh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvidenceFindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "to_status",
                table: "participation_history",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "from_status",
                table: "participation_history",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.CreateTable(
                name: "submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    participation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contributor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    summary = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    scope_completed = table.Column<bool>(type: "boolean", nullable: false),
                    revision_number = table.Column<int>(type: "integer", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    correction_requested_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    dispute_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    correction_deadline = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    bonus_granted = table.Column<long>(type: "bigint", nullable: false),
                    settled_credits = table.Column<long>(type: "bigint", nullable: false),
                    auto_accepted = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_submissions_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contributor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    rejection_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    scanned_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attachments", x => x.id);
                    table.ForeignKey(
                        name: "FK_attachments_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "findings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contributor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    severity = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    title = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    reproduction = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    expected_result = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    actual_result = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    decision_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_findings", x => x.id);
                    table.ForeignKey(
                        name: "FK_findings_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    to_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_submission_history_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_revisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    summary = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    scope_completed = table.Column<bool>(type: "boolean", nullable: false),
                    step_results = table.Column<string>(type: "jsonb", nullable: false),
                    tested_areas = table.Column<string>(type: "jsonb", nullable: false),
                    clean_result_explanation = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_revisions", x => x.id);
                    table.ForeignKey(
                        name: "FK_submission_revisions_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "finding_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    finding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    to_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finding_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_finding_history_findings_finding_id",
                        column: x => x.finding_id,
                        principalTable: "findings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attachments_submission_id",
                table: "attachments",
                column: "submission_id");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_revision_id",
                table: "attachments",
                column: "revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_state_created_at",
                table: "attachments",
                columns: new[] { "state", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_finding_history_finding_id_changed_at",
                table: "finding_history",
                columns: new[] { "finding_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_findings_state_severity",
                table: "findings",
                columns: new[] { "state", "severity" });

            migrationBuilder.CreateIndex(
                name: "ix_findings_submission_id_created_at",
                table: "findings",
                columns: new[] { "submission_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_submission_history_submission_id_changed_at",
                table: "submission_history",
                columns: new[] { "submission_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ux_submission_revisions_submission_id_number",
                table: "submission_revisions",
                columns: new[] { "submission_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_submissions_task_id",
                table: "submissions",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "ix_submissions_contributor_id_created_at",
                table: "submissions",
                columns: new[] { "contributor_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_submissions_state_submitted_at",
                table: "submissions",
                columns: new[] { "state", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "ux_submissions_participation_id",
                table: "submissions",
                column: "participation_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attachments");

            migrationBuilder.DropTable(
                name: "finding_history");

            migrationBuilder.DropTable(
                name: "submission_history");

            migrationBuilder.DropTable(
                name: "submission_revisions");

            migrationBuilder.DropTable(
                name: "findings");

            migrationBuilder.DropTable(
                name: "submissions");

            migrationBuilder.AlterColumn<string>(
                name: "to_status",
                table: "participation_history",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "from_status",
                table: "participation_history",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);
        }
    }
}
