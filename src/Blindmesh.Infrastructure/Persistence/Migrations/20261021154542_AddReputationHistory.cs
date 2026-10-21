using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blindmesh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReputationHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reputation_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    delta = table.Column<int>(type: "integer", nullable: false),
                    source_participation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_submission_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_finding_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reputation_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reputation_scores",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accepted_contributions = table.Column<int>(type: "integer", nullable: false),
                    confirmed_findings = table.Column<int>(type: "integer", nullable: false),
                    reversed_contributions = table.Column<int>(type: "integer", nullable: false),
                    current_score = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reputation_scores", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "reputation_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_score = table.Column<int>(type: "integer", nullable: false),
                    accepted_contributions = table.Column<int>(type: "integer", nullable: false),
                    confirmed_findings = table.Column<int>(type: "integer", nullable: false),
                    reversed_contributions = table.Column<int>(type: "integer", nullable: false),
                    taken_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reputation_snapshots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reputation_events_user_id_occurred_at",
                table: "reputation_events",
                columns: new[] { "user_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ux_reputation_events_source_event_id",
                table: "reputation_events",
                column: "source_event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reputation_snapshots_user_id_taken_at",
                table: "reputation_snapshots",
                columns: new[] { "user_id", "taken_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reputation_events");

            migrationBuilder.DropTable(
                name: "reputation_scores");

            migrationBuilder.DropTable(
                name: "reputation_snapshots");
        }
    }
}
