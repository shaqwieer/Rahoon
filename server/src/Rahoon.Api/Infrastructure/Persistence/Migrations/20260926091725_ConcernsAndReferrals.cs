using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rahoon.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConcernsAndReferrals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "request_concerns",
                schema: "requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subject = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    response_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    responded_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    responded_by_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_request_concerns", x => x.id);
                    table.ForeignKey(
                        name: "fk_request_concerns_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_concerns_requests_request_id",
                        column: x => x.request_id,
                        principalSchema: "requests",
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "specialist_referrals",
                schema: "requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    specialist_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    specialist_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    applicant_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_by_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_specialist_referrals", x => x.id);
                    table.ForeignKey(
                        name: "fk_specialist_referrals_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_specialist_referrals_requests_request_id",
                        column: x => x.request_id,
                        principalSchema: "requests",
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_request_concerns_organization_id_status",
                schema: "requests",
                table: "request_concerns",
                columns: new[] { "organization_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_request_concerns_reference",
                schema: "requests",
                table: "request_concerns",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_request_concerns_request_id",
                schema: "requests",
                table: "request_concerns",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "ix_specialist_referrals_organization_id",
                schema: "requests",
                table: "specialist_referrals",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_specialist_referrals_request_id",
                schema: "requests",
                table: "specialist_referrals",
                column: "request_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "request_concerns",
                schema: "requests");

            migrationBuilder.DropTable(
                name: "specialist_referrals",
                schema: "requests");
        }
    }
}
