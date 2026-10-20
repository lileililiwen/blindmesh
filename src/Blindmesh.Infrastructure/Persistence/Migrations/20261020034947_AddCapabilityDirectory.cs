using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blindmesh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCapabilityDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "capabilities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    visibility = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    available = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    verification = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_capabilities", x => x.id);
                    table.ForeignKey(
                        name: "FK_capabilities_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "capability_status_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    capability_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    to_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_capability_status_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_capability_status_history_capabilities_capability_id",
                        column: x => x.capability_id,
                        principalTable: "capabilities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_capability_status_history_users_changed_by",
                        column: x => x.changed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "capability_tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    capability_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_capability_tags", x => x.id);
                    table.ForeignKey(
                        name: "FK_capability_tags_capabilities_capability_id",
                        column: x => x.capability_id,
                        principalTable: "capabilities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_capabilities_created_at_id",
                table: "capabilities",
                columns: new[] { "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_capabilities_owner_id_created_at",
                table: "capabilities",
                columns: new[] { "owner_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_capabilities_visibility_available_status",
                table: "capabilities",
                columns: new[] { "visibility", "available", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_capability_status_history_changed_by",
                table: "capability_status_history",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_capability_status_history_capability_id_changed_at",
                table: "capability_status_history",
                columns: new[] { "capability_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ux_capability_tags_capability_id_tag",
                table: "capability_tags",
                columns: new[] { "capability_id", "tag" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "capability_status_history");

            migrationBuilder.DropTable(
                name: "capability_tags");

            migrationBuilder.DropTable(
                name: "capabilities");
        }
    }
}
