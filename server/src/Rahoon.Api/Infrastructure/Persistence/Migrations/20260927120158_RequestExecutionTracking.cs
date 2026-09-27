using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rahoon.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequestExecutionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "request_execution_records",
                schema: "requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    source_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    share_source_with_applicant = table.Column<bool>(type: "boolean", nullable: false),
                    lender_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    lender_date = table.Column<DateOnly>(type: "date", nullable: false),
                    summary_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    explanation_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    offer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    path = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    activation_date = table.Column<DateOnly>(type: "date", nullable: true),
                    new_installment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    term_months = table.Column<int>(type: "integer", nullable: true),
                    settlement_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    terms_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    received_on = table.Column<DateOnly>(type: "date", nullable: true),
                    schedule_item_no = table.Column<int>(type: "integer", nullable: true),
                    answers_report_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notice_category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    document_kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    supersedes_record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correction_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("pk_request_execution_records", x => x.id);
                    table.CheckConstraint("ck_request_execution_records_verifier_not_recorder", "verified_by_user_id IS NULL OR verified_by_user_id <> recorded_by_user_id");
                    table.ForeignKey(
                        name: "fk_request_execution_records_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_execution_records_request_documents_source_document",
                        column: x => x.source_document_id,
                        principalSchema: "requests",
                        principalTable: "request_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_execution_records_requests_request_id",
                        column: x => x.request_id,
                        principalSchema: "requests",
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "request_payment_reports",
                schema: "requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    transfer_date = table.Column<DateOnly>(type: "date", nullable: false),
                    bank_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    schedule_item_no = table.Column<int>(type: "integer", nullable: true),
                    proof_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    team_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    confirmation_record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    answered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_request_payment_reports", x => x.id);
                    table.ForeignKey(
                        name: "fk_request_payment_reports_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_payment_reports_request_documents_proof_document_id",
                        column: x => x.proof_document_id,
                        principalSchema: "requests",
                        principalTable: "request_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_payment_reports_requests_request_id",
                        column: x => x.request_id,
                        principalSchema: "requests",
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "request_schedule_items",
                schema: "requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    no = table.Column<int>(type: "integer", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_request_schedule_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_request_schedule_items_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_schedule_items_request_execution_records_record_id",
                        column: x => x.record_id,
                        principalSchema: "requests",
                        principalTable: "request_execution_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_request_execution_records_organization_id_status",
                schema: "requests",
                table: "request_execution_records",
                columns: new[] { "organization_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_request_execution_records_request_id_kind_status",
                schema: "requests",
                table: "request_execution_records",
                columns: new[] { "request_id", "kind", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_request_execution_records_source_document_id",
                schema: "requests",
                table: "request_execution_records",
                column: "source_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_payment_reports_organization_id",
                schema: "requests",
                table: "request_payment_reports",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_payment_reports_proof_document_id",
                schema: "requests",
                table: "request_payment_reports",
                column: "proof_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_payment_reports_reference",
                schema: "requests",
                table: "request_payment_reports",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_request_payment_reports_request_id",
                schema: "requests",
                table: "request_payment_reports",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_schedule_items_organization_id",
                schema: "requests",
                table: "request_schedule_items",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_schedule_items_record_id_no",
                schema: "requests",
                table: "request_schedule_items",
                columns: new[] { "record_id", "no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_request_schedule_items_request_id",
                schema: "requests",
                table: "request_schedule_items",
                column: "request_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "request_payment_reports",
                schema: "requests");

            migrationBuilder.DropTable(
                name: "request_schedule_items",
                schema: "requests");

            migrationBuilder.DropTable(
                name: "request_execution_records",
                schema: "requests");
        }
    }
}
