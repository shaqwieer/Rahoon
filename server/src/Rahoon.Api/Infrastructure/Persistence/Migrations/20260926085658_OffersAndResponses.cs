using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rahoon.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OffersAndResponses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "request_offers",
                schema: "requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    path = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    new_installment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    term_months = table.Column<int>(type: "integer", nullable: true),
                    start_text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    settlement_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    payment_conditions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    remaining_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    sale_terms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    conditions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    effect_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    lender_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    lender_letter_date = table.Column<DateOnly>(type: "date", nullable: false),
                    lender_validity_text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    letter_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    share_letter_with_applicant = table.Column<bool>(type: "boolean", nullable: false),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_by_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    verified_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_by_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verification_checklist = table.Column<List<string>>(type: "text[]", nullable: false),
                    return_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_request_offers", x => x.id);
                    table.CheckConstraint("ck_request_offers_verifier_not_recorder", "verified_by_user_id IS NULL OR verified_by_user_id <> recorded_by_user_id");
                    table.ForeignKey(
                        name: "fk_request_offers_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_offers_request_documents_letter_document_id",
                        column: x => x.letter_document_id,
                        principalSchema: "requests",
                        principalTable: "request_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_offers_requests_request_id",
                        column: x => x.request_id,
                        principalSchema: "requests",
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "request_responses",
                schema: "requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    offer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    consent_text_snapshot = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    otp_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ip_masked = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    relayed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    relayed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    relay_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_request_responses", x => x.id);
                    table.ForeignKey(
                        name: "fk_request_responses_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_responses_request_offers_offer_id",
                        column: x => x.offer_id,
                        principalSchema: "requests",
                        principalTable: "request_offers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_responses_requests_request_id",
                        column: x => x.request_id,
                        principalSchema: "requests",
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_request_offers_letter_document_id",
                schema: "requests",
                table: "request_offers",
                column: "letter_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_offers_organization_id",
                schema: "requests",
                table: "request_offers",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_offers_request_id_version_no",
                schema: "requests",
                table: "request_offers",
                columns: new[] { "request_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_request_responses_offer_id",
                schema: "requests",
                table: "request_responses",
                column: "offer_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_responses_organization_id",
                schema: "requests",
                table: "request_responses",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_responses_reference",
                schema: "requests",
                table: "request_responses",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_request_responses_request_id",
                schema: "requests",
                table: "request_responses",
                column: "request_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "request_responses",
                schema: "requests");

            migrationBuilder.DropTable(
                name: "request_offers",
                schema: "requests");
        }
    }
}
