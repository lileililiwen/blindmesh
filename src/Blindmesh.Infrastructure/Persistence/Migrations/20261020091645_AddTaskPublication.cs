using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blindmesh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskPublication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    scope = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    modes = table.Column<int>(type: "integer", nullable: false),
                    visibility = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    expected_minutes = table.Column<int>(type: "integer", nullable: false),
                    base_reward = table.Column<long>(type: "bigint", nullable: false),
                    slot_bonus = table.Column<long>(type: "bigint", nullable: false),
                    slot_count = table.Column<int>(type: "integer", nullable: false),
                    deadline = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    accepts_clean_findings = table.Column<bool>(type: "boolean", nullable: false),
                    access_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    access_resource = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    review_criteria = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    reserved_budget = table.Column<long>(type: "bigint", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks", x => x.id);
                    table.ForeignKey(
                        name: "FK_tasks_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tasks_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    to_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_task_history_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_requirements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_requirements", x => x.id);
                    table.ForeignKey(
                        name: "FK_task_requirements_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_slots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    reward = table.Column<long>(type: "bigint", nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    contributor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_slots", x => x.id);
                    table.ForeignKey(
                        name: "FK_task_slots_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_steps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    expected_result = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_steps", x => x.id);
                    table.ForeignKey(
                        name: "FK_task_steps_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_task_history_task_id_changed_at",
                table: "task_history",
                columns: new[] { "task_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_task_requirements_kind_value_task_id",
                table: "task_requirements",
                columns: new[] { "kind", "value", "task_id" });

            migrationBuilder.CreateIndex(
                name: "ix_task_requirements_task_id",
                table: "task_requirements",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_slots_task_id_position",
                table: "task_slots",
                columns: new[] { "task_id", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_task_steps_task_id_position",
                table: "task_steps",
                columns: new[] { "task_id", "position" });

            migrationBuilder.CreateIndex(
                name: "IX_tasks_owner_id",
                table: "tasks",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_tasks_created_at_id",
                table: "tasks",
                columns: new[] { "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_tasks_project_id_created_at",
                table: "tasks",
                columns: new[] { "project_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_tasks_status_modes",
                table: "tasks",
                columns: new[] { "status", "modes" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_history");

            migrationBuilder.DropTable(
                name: "task_requirements");

            migrationBuilder.DropTable(
                name: "task_slots");

            migrationBuilder.DropTable(
                name: "task_steps");

            migrationBuilder.DropTable(
                name: "tasks");
        }
    }
}
