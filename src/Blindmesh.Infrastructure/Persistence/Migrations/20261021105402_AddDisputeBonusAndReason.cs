using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blindmesh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDisputeBonusAndReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "awarded_amount",
                table: "disputes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "evidence_note",
                table: "disputes",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "awarded_amount",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "evidence_note",
                table: "disputes");
        }
    }
}
