using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blindmesh.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "credit_lots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    original_amount = table.Column<long>(type: "bigint", nullable: false),
                    available_amount = table.Column<long>(type: "bigint", nullable: false),
                    reserved_amount = table.Column<long>(type: "bigint", nullable: false),
                    pending_amount = table.Column<long>(type: "bigint", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_lots", x => x.id);
                    table.ForeignKey(
                        name: "FK_credit_lots_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ledger_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bucket = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_ledger_accounts_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ledger_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_ledger_transactions_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "grant_claims",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    subject = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    rules_accepted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grant_claims", x => x.id);
                    table.ForeignKey(
                        name: "FK_grant_claims_ledger_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "ledger_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_grant_claims_ledger_transactions_transaction_id",
                        column: x => x.transaction_id,
                        principalTable: "ledger_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_grant_claims_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ledger_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_ledger_entries_credit_lots_lot_id",
                        column: x => x.lot_id,
                        principalTable: "credit_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ledger_entries_ledger_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "ledger_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ledger_entries_ledger_transactions_transaction_id",
                        column: x => x.transaction_id,
                        principalTable: "ledger_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_credit_lots_user_id_created_at",
                table: "credit_lots",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_credit_lots_pending_user_id_reference",
                table: "credit_lots",
                columns: new[] { "user_id", "reference" },
                unique: true,
                filter: "source = 'pending'");

            migrationBuilder.CreateIndex(
                name: "IX_grant_claims_account_id",
                table: "grant_claims",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_grant_claims_transaction_id",
                table: "grant_claims",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_grant_claims_user_id",
                table: "grant_claims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_grant_claims_ip_address_issued_at",
                table: "grant_claims",
                columns: new[] { "ip_address", "issued_at" });

            migrationBuilder.CreateIndex(
                name: "ix_grant_claims_issued_at",
                table: "grant_claims",
                column: "issued_at");

            migrationBuilder.CreateIndex(
                name: "ux_grant_claims_provider_subject",
                table: "grant_claims",
                columns: new[] { "provider", "subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ledger_accounts_owner_id",
                table: "ledger_accounts",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ux_ledger_accounts_bucket_owner_id_reference",
                table: "ledger_accounts",
                columns: new[] { "bucket", "owner_id", "reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_ledger_accounts_platform_bucket_reference",
                table: "ledger_accounts",
                columns: new[] { "bucket", "reference" },
                unique: true,
                filter: "owner_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_lot_id",
                table: "ledger_entries",
                column: "lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_account_id_created_at",
                table: "ledger_entries",
                columns: new[] { "account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_transaction_id",
                table: "ledger_entries",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_transactions_actor_id",
                table: "ledger_transactions",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_transactions_created_at_id",
                table: "ledger_transactions",
                columns: new[] { "created_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "grant_claims");

            migrationBuilder.DropTable(
                name: "ledger_entries");

            migrationBuilder.DropTable(
                name: "credit_lots");

            migrationBuilder.DropTable(
                name: "ledger_accounts");

            migrationBuilder.DropTable(
                name: "ledger_transactions");
        }
    }
}
