using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blindmesh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddParticipationExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contributor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capability_snapshot = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    scope_consent = table.Column<bool>(type: "boolean", nullable: false),
                    expected_version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_applications", x => x.id);
                    table.ForeignKey(
                        name: "FK_applications_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "participations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contributor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    slot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_id = table.Column<Guid>(type: "uuid", nullable: true),
                    applied_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    execution_deadline = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participations", x => x.id);
                    table.ForeignKey(
                        name: "FK_participations_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "slot_leases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contributor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    acquired_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    released_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_slot_leases", x => x.id);
                    table.ForeignKey(
                        name: "FK_slot_leases_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "participation_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    participation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    to_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participation_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_participation_history_participations_participation_id",
                        column: x => x.participation_id,
                        principalTable: "participations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_applications_task_id_contributor_id",
                table: "applications",
                columns: new[] { "task_id", "contributor_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_participation_history_participation_id_changed_at",
                table: "participation_history",
                columns: new[] { "participation_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_participations_contributor_id_created_at",
                table: "participations",
                columns: new[] { "contributor_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_participations_state_applied_at",
                table: "participations",
                columns: new[] { "state", "applied_at" });

            migrationBuilder.CreateIndex(
                name: "ux_participations_application_id",
                table: "participations",
                column: "application_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_participations_task_id_contributor_id",
                table: "participations",
                columns: new[] { "task_id", "contributor_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_slot_leases_task_id",
                table: "slot_leases",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "ux_slot_leases_participation_id_live",
                table: "slot_leases",
                column: "participation_id",
                unique: true,
                filter: "released_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_slot_leases_slot_id_live",
                table: "slot_leases",
                column: "slot_id",
                unique: true,
                filter: "released_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "applications");

            migrationBuilder.DropTable(
                name: "participation_history");

            migrationBuilder.DropTable(
                name: "slot_leases");

            migrationBuilder.DropTable(
                name: "participations");
        }
    }
}
