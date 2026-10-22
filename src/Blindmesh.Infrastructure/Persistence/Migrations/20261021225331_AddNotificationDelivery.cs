using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blindmesh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    unique_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    summary = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    deep_link = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    last_error = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    read_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    dead_lettered_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_deliveries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    in_app_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    email_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_preferences", x => x.user_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_recipient_created_at",
                table: "notification_deliveries",
                columns: new[] { "recipient_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_status_next_attempt_at",
                table: "notification_deliveries",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ux_notification_deliveries_unique_key",
                table: "notification_deliveries",
                column: "unique_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_deliveries");

            migrationBuilder.DropTable(
                name: "notification_preferences");
        }
    }
}
