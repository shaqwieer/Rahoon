using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rahoon.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IndividualRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "requests");

            migrationBuilder.AddColumn<int>(
                name: "hash_version",
                schema: "audit",
                table: "audit_events",
                type: "integer",
                nullable: false,
                // Existing events keep the original canonical hash (format 1) so the chain still verifies.
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "subject_reference",
                schema: "audit",
                table: "audit_events",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "subject_type",
                schema: "audit",
                table: "audit_events",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "financing_institutions",
                schema: "requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    linked_organization_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_financing_institutions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "requests",
                schema: "requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    waiting_on = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    next_step_text = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    status_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    assigned_coordinator_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status_before_info_request = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: true),
                    info_request_is_consent = table.Column<bool>(type: "boolean", nullable: false),
                    institution_id = table.Column<Guid>(type: "uuid", nullable: true),
                    institution_other_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    applicant_full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    contract_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    monthly_installment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    arrears_duration = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    property_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    path_preference = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: true),
                    affordable_monthly = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    situation_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    duplicate_of_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    duplicate_acknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    withdrawn_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    withdraw_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    outcome_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    outcome_summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    not_eligible_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_requests_financing_institutions_institution_id",
                        column: x => x.institution_id,
                        principalSchema: "requests",
                        principalTable: "financing_institutions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_requests_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_requests_users_applicant_user_id",
                        column: x => x.applicant_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "request_consents",
                schema: "requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text_version = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    text_snapshot = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    institution_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recipient_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    data_categories = table.Column<List<string>>(type: "text[]", nullable: false),
                    otp_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ip_masked = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    withdrawn_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    withdrawn_reason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_request_consents", x => x.id);
                    table.ForeignKey(
                        name: "fk_request_consents_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_consents_requests_request_id",
                        column: x => x.request_id,
                        principalSchema: "requests",
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "request_documents",
                schema: "requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    visibility = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    added_after_submit = table.Column<bool>(type: "boolean", nullable: false),
                    version_count = table.Column<int>(type: "integer", nullable: false),
                    current_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_request_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_request_documents_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_documents_requests_request_id",
                        column: x => x.request_id,
                        principalSchema: "requests",
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "request_updates",
                schema: "requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    author_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    author_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    visible_to_applicant = table.Column<bool>(type: "boolean", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_request_updates", x => x.id);
                    table.ForeignKey(
                        name: "fk_request_updates_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_updates_requests_request_id",
                        column: x => x.request_id,
                        principalSchema: "requests",
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "request_document_versions",
                schema: "requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_by_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    scan_status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_request_document_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_request_document_versions_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_document_versions_request_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "requests",
                        principalTable: "request_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_subject_reference_seq",
                schema: "audit",
                table: "audit_events",
                columns: new[] { "subject_reference", "seq" });

            migrationBuilder.CreateIndex(
                name: "ix_financing_institutions_name_ar",
                schema: "requests",
                table: "financing_institutions",
                column: "name_ar",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_request_consents_organization_id",
                schema: "requests",
                table: "request_consents",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_consents_request_id",
                schema: "requests",
                table: "request_consents",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_document_versions_document_id_version_no",
                schema: "requests",
                table: "request_document_versions",
                columns: new[] { "document_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_request_document_versions_organization_id",
                schema: "requests",
                table: "request_document_versions",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_document_versions_request_id",
                schema: "requests",
                table: "request_document_versions",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_documents_organization_id",
                schema: "requests",
                table: "request_documents",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_documents_request_id",
                schema: "requests",
                table: "request_documents",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_updates_organization_id",
                schema: "requests",
                table: "request_updates",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_updates_request_id_at",
                schema: "requests",
                table: "request_updates",
                columns: new[] { "request_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_requests_applicant_user_id_created_at",
                schema: "requests",
                table: "requests",
                columns: new[] { "applicant_user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_requests_institution_id",
                schema: "requests",
                table: "requests",
                column: "institution_id");

            migrationBuilder.CreateIndex(
                name: "ix_requests_organization_id_status",
                schema: "requests",
                table: "requests",
                columns: new[] { "organization_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_requests_reference",
                schema: "requests",
                table: "requests",
                column: "reference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "request_consents",
                schema: "requests");

            migrationBuilder.DropTable(
                name: "request_document_versions",
                schema: "requests");

            migrationBuilder.DropTable(
                name: "request_updates",
                schema: "requests");

            migrationBuilder.DropTable(
                name: "request_documents",
                schema: "requests");

            migrationBuilder.DropTable(
                name: "requests",
                schema: "requests");

            migrationBuilder.DropTable(
                name: "financing_institutions",
                schema: "requests");

            migrationBuilder.DropIndex(
                name: "ix_audit_events_subject_reference_seq",
                schema: "audit",
                table: "audit_events");

            migrationBuilder.DropColumn(
                name: "hash_version",
                schema: "audit",
                table: "audit_events");

            migrationBuilder.DropColumn(
                name: "subject_reference",
                schema: "audit",
                table: "audit_events");

            migrationBuilder.DropColumn(
                name: "subject_type",
                schema: "audit",
                table: "audit_events");
        }
    }
}
