using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rahoon.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OwnerPortalAndHardship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hardship_requests",
                schema: "comms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason_key = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    callback_requested = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hardship_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_hardship_requests_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_hardship_requests_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_notices",
                schema: "comms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installment_no = table.Column<int>(type: "integer", nullable: false),
                    transfer_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    reference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_notices", x => x.id);
                    table.ForeignKey(
                        name: "fk_payment_notices_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payment_notices_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_hardship_requests_case_id",
                schema: "comms",
                table: "hardship_requests",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_hardship_requests_organization_id",
                schema: "comms",
                table: "hardship_requests",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_notices_case_id",
                schema: "comms",
                table: "payment_notices",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_notices_organization_id",
                schema: "comms",
                table: "payment_notices",
                column: "organization_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hardship_requests",
                schema: "comms");

            migrationBuilder.DropTable(
                name: "payment_notices",
                schema: "comms");
        }
    }
}
