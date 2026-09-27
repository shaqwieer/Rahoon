using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rahoon.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReferralClosureAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "analytics");

            migrationBuilder.AddColumn<string>(
                name: "approval_reason",
                schema: "closure",
                table: "reconciliations",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "expected_source",
                schema: "closure",
                table: "reconciliations",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "note",
                schema: "closure",
                table: "reconciliations",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "return_reason",
                schema: "closure",
                table: "reconciliations",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "review_reason",
                schema: "closure",
                table: "reconciliations",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reviewed_at",
                schema: "closure",
                table: "reconciliations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "submitted_at",
                schema: "closure",
                table: "reconciliations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_type",
                schema: "closure",
                table: "reconciliation_lines",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "value_date",
                schema: "closure",
                table: "reconciliation_lines",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "decided_at",
                schema: "referral",
                table: "judicial_referrals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "decided_by_user_id",
                schema: "referral",
                table: "judicial_referrals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "external_reference_entered_at",
                schema: "referral",
                table: "judicial_referrals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "external_reference_entered_by_user_id",
                schema: "referral",
                table: "judicial_referrals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_reference_source",
                schema: "referral",
                table: "judicial_referrals",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "notice_sent_by_user_id",
                schema: "referral",
                table: "judicial_referrals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notice_template_code",
                schema: "referral",
                table: "judicial_referrals",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "requested_at",
                schema: "referral",
                table: "judicial_referrals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                schema: "referral",
                table: "external_status_entries",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "officially_confirmed",
                schema: "referral",
                table: "external_status_entries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "source_kind",
                schema: "referral",
                table: "external_status_entries",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "share_with_owner",
                schema: "closure",
                table: "closure_documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "agent_updates",
                schema: "referral",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_label = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    text = table.Column<string>(type: "text", maxLength: 2000, nullable: false),
                    kind = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attachment_version_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_updates", x => x.id);
                    table.ForeignKey(
                        name: "fk_agent_updates_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_agent_updates_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "checklist_evidence",
                schema: "referral",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    document_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_checklist_evidence", x => x.id);
                    table.ForeignKey(
                        name: "fk_checklist_evidence_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_checklist_evidence_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "closure_requests",
                schema: "closure",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reconciliation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "text", maxLength: 2000, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    trace_json = table.Column<string>(type: "jsonb", maxLength: 2000, nullable: false),
                    trace_sha256 = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    trace_acknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_closure_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_closure_requests_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_closure_requests_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "distributions",
                schema: "closure",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reconciliation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    procedure_costs = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    net_proceeds = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    debt_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    debt_basis_ref = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    lender_share = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    other_fees = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    owner_surplus = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    shortfall = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    surplus_destination_masked = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    prepared_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    checked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    check_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approval_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    return_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    executed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_distributions", x => x.id);
                    table.CheckConstraint("ck_distribution_duties", "(checked_by_user_id IS NULL OR checked_by_user_id <> prepared_by_user_id) AND (approved_by_user_id IS NULL OR (approved_by_user_id <> prepared_by_user_id AND approved_by_user_id <> checked_by_user_id))");
                    table.CheckConstraint("ck_distribution_net", "net_proceeds = sale_price - procedure_costs");
                    table.CheckConstraint("ck_distribution_waterfall", "net_proceeds = lender_share + other_fees + owner_surplus");
                    table.ForeignKey(
                        name: "fk_distributions_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_distributions_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_draft_versions",
                schema: "analytics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    body = table.Column<string>(type: "text", maxLength: 2000, nullable: false),
                    by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    by_label = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_draft_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_document_draft_versions_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_drafts",
                schema: "analytics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    template_code = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    template_version = table.Column<int>(type: "integer", nullable: false),
                    model_version = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    generated_body = table.Column<string>(type: "text", maxLength: 2000, nullable: false),
                    current_body = table.Column<string>(type: "text", maxLength: 2000, nullable: false),
                    filled_values_json = table.Column<string>(type: "jsonb", maxLength: 2000, nullable: false),
                    flags_json = table.Column<string>(type: "jsonb", maxLength: 2000, nullable: false),
                    inputs_json = table.Column<string>(type: "jsonb", maxLength: 2000, nullable: false),
                    confidence = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    limitations = table.Column<string>(type: "text", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    human_review = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    review_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    attached_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attached_to = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_drafts", x => x.id);
                    table.ForeignKey(
                        name: "fk_document_drafts_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_document_drafts_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evidence_packs",
                schema: "referral",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    referral_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    seq_no = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    manifest_sha256 = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    mappings_json = table.Column<string>(type: "jsonb", maxLength: 2000, nullable: false),
                    built_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    built_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    locked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    exported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    exported_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evidence_packs", x => x.id);
                    table.ForeignKey(
                        name: "fk_evidence_packs_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_evidence_packs_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exceptions",
                schema: "referral",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    referral_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    type = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    title = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    description = table.Column<string>(type: "text", maxLength: 2000, nullable: false),
                    platform_state = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    external_state_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    due_on = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resolution_action = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    resolution_note = table.Column<string>(type: "text", maxLength: 2000, nullable: true),
                    resolved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exceptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_exceptions_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exceptions_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "insight_feedback",
                schema: "analytics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    insight_key = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    model_version = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_insight_feedback", x => x.id);
                    table.ForeignKey(
                        name: "fk_insight_feedback_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "operational_settings",
                schema: "analytics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    value_json = table.Column<string>(type: "jsonb", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    proposed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operational_settings", x => x.id);
                    table.CheckConstraint("ck_setting_second_person", "decided_by_user_id IS NULL OR decided_by_user_id <> proposed_by_user_id");
                    table.ForeignKey(
                        name: "fk_operational_settings_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prediction_opinions",
                schema: "analytics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    prediction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    override_direction = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    reason = table.Column<string>(type: "text", maxLength: 2000, nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prediction_opinions", x => x.id);
                    table.CheckConstraint("ck_opinion_override_reason", "value <> 'override' OR (reason IS NOT NULL AND length(btrim(reason)) > 0)");
                    table.ForeignKey(
                        name: "fk_prediction_opinions_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prediction_opinions_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "predictions",
                schema: "analytics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_ref = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    model_id = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    model_version = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    question = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    score_low = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    score_point = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    score_high = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    confidence = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    confidence_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    factors_json = table.Column<string>(type: "jsonb", maxLength: 2000, nullable: false),
                    inputs_json = table.Column<string>(type: "jsonb", maxLength: 2000, nullable: false),
                    excluded_attributes = table.Column<List<string>>(type: "text[]", nullable: false),
                    limitations = table.Column<string>(type: "text", maxLength: 2000, nullable: false),
                    data_as_of = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_predictions", x => x.id);
                    table.ForeignKey(
                        name: "fk_predictions_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_predictions_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sale_plan_milestones",
                schema: "referral",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seq = table.Column<int>(type: "integer", nullable: false),
                    on = table.Column<DateOnly>(type: "date", nullable: false),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sale_plan_milestones", x => x.id);
                    table.ForeignKey(
                        name: "fk_sale_plan_milestones_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sale_plan_milestones_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sale_results",
                schema: "referral",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    official_sale_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    sale_minutes_date = table.Column<DateOnly>(type: "date", nullable: true),
                    declared_costs = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    evidence_version_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    source = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    submitted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmation_source = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    official_confirmation_date = table.Column<DateOnly>(type: "date", nullable: true),
                    confirmation_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sale_results", x => x.id);
                    table.CheckConstraint("ck_sale_result_amounts", "(official_sale_price IS NULL OR official_sale_price > 0) AND (declared_costs IS NULL OR declared_costs >= 0)");
                    table.ForeignKey(
                        name: "fk_sale_results_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sale_results_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "distribution_lines",
                schema: "closure",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    distribution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seq = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    label = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    basis_ref = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    destination_masked = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    executed_txn_ref = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    executed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_distribution_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_distribution_lines_distributions_distribution_id",
                        column: x => x.distribution_id,
                        principalSchema: "closure",
                        principalTable: "distributions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_distribution_lines_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evidence_pack_items",
                schema: "referral",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pack_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seq = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    source_type = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    source_ref = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    version_label = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    sha256 = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    verified = table.Column<bool>(type: "boolean", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evidence_pack_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_evidence_pack_items_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_evidence_pack_items_evidence_packs_pack_id",
                        column: x => x.pack_id,
                        principalSchema: "referral",
                        principalTable: "evidence_packs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_evidence_pack_items_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_updates_case_id",
                schema: "referral",
                table: "agent_updates",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_updates_organization_id",
                schema: "referral",
                table: "agent_updates",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_checklist_evidence_case_id",
                schema: "referral",
                table: "checklist_evidence",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_checklist_evidence_organization_id",
                schema: "referral",
                table: "checklist_evidence",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_closure_requests_case_id",
                schema: "closure",
                table: "closure_requests",
                column: "case_id",
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_closure_requests_organization_id",
                schema: "closure",
                table: "closure_requests",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_distribution_lines_distribution_id",
                schema: "closure",
                table: "distribution_lines",
                column: "distribution_id");

            migrationBuilder.CreateIndex(
                name: "ix_distribution_lines_organization_id",
                schema: "closure",
                table: "distribution_lines",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_distributions_case_id",
                schema: "closure",
                table: "distributions",
                column: "case_id",
                unique: true,
                filter: "status <> 'Returned'");

            migrationBuilder.CreateIndex(
                name: "ix_distributions_organization_id",
                schema: "closure",
                table: "distributions",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_draft_versions_draft_id_version_no",
                schema: "analytics",
                table: "document_draft_versions",
                columns: new[] { "draft_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_draft_versions_organization_id",
                schema: "analytics",
                table: "document_draft_versions",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_drafts_case_id",
                schema: "analytics",
                table: "document_drafts",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_drafts_organization_id",
                schema: "analytics",
                table: "document_drafts",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidence_pack_items_case_id",
                schema: "referral",
                table: "evidence_pack_items",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidence_pack_items_organization_id",
                schema: "referral",
                table: "evidence_pack_items",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidence_pack_items_pack_id_seq",
                schema: "referral",
                table: "evidence_pack_items",
                columns: new[] { "pack_id", "seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evidence_packs_case_id",
                schema: "referral",
                table: "evidence_packs",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidence_packs_case_id_seq_no",
                schema: "referral",
                table: "evidence_packs",
                columns: new[] { "case_id", "seq_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evidence_packs_organization_id",
                schema: "referral",
                table: "evidence_packs",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidence_packs_reference",
                schema: "referral",
                table: "evidence_packs",
                column: "reference");

            migrationBuilder.CreateIndex(
                name: "ix_exceptions_case_id",
                schema: "referral",
                table: "exceptions",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_exceptions_organization_id",
                schema: "referral",
                table: "exceptions",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_exceptions_reference",
                schema: "referral",
                table: "exceptions",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_insight_feedback_organization_id",
                schema: "analytics",
                table: "insight_feedback",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_operational_settings_organization_id_key",
                schema: "analytics",
                table: "operational_settings",
                columns: new[] { "organization_id", "key" },
                unique: true,
                filter: "status = 'Effective'");

            migrationBuilder.CreateIndex(
                name: "ix_operational_settings_organization_id_key_version_no",
                schema: "analytics",
                table: "operational_settings",
                columns: new[] { "organization_id", "key", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_operational_settings_organization_id_key1",
                schema: "analytics",
                table: "operational_settings",
                columns: new[] { "organization_id", "key" },
                unique: true,
                filter: "status = 'PendingApproval'");

            migrationBuilder.CreateIndex(
                name: "ix_prediction_opinions_case_id",
                schema: "analytics",
                table: "prediction_opinions",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_prediction_opinions_organization_id",
                schema: "analytics",
                table: "prediction_opinions",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_predictions_case_id",
                schema: "analytics",
                table: "predictions",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_predictions_organization_id",
                schema: "analytics",
                table: "predictions",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_plan_milestones_case_id",
                schema: "referral",
                table: "sale_plan_milestones",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_plan_milestones_organization_id",
                schema: "referral",
                table: "sale_plan_milestones",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_results_assignment_id",
                schema: "referral",
                table: "sale_results",
                column: "assignment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sale_results_case_id",
                schema: "referral",
                table: "sale_results",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_results_organization_id",
                schema: "referral",
                table: "sale_results",
                column: "organization_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_updates",
                schema: "referral");

            migrationBuilder.DropTable(
                name: "checklist_evidence",
                schema: "referral");

            migrationBuilder.DropTable(
                name: "closure_requests",
                schema: "closure");

            migrationBuilder.DropTable(
                name: "distribution_lines",
                schema: "closure");

            migrationBuilder.DropTable(
                name: "document_draft_versions",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "document_drafts",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "evidence_pack_items",
                schema: "referral");

            migrationBuilder.DropTable(
                name: "exceptions",
                schema: "referral");

            migrationBuilder.DropTable(
                name: "insight_feedback",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "operational_settings",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "prediction_opinions",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "predictions",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "sale_plan_milestones",
                schema: "referral");

            migrationBuilder.DropTable(
                name: "sale_results",
                schema: "referral");

            migrationBuilder.DropTable(
                name: "distributions",
                schema: "closure");

            migrationBuilder.DropTable(
                name: "evidence_packs",
                schema: "referral");

            migrationBuilder.DropColumn(
                name: "approval_reason",
                schema: "closure",
                table: "reconciliations");

            migrationBuilder.DropColumn(
                name: "expected_source",
                schema: "closure",
                table: "reconciliations");

            migrationBuilder.DropColumn(
                name: "note",
                schema: "closure",
                table: "reconciliations");

            migrationBuilder.DropColumn(
                name: "return_reason",
                schema: "closure",
                table: "reconciliations");

            migrationBuilder.DropColumn(
                name: "review_reason",
                schema: "closure",
                table: "reconciliations");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                schema: "closure",
                table: "reconciliations");

            migrationBuilder.DropColumn(
                name: "submitted_at",
                schema: "closure",
                table: "reconciliations");

            migrationBuilder.DropColumn(
                name: "source_type",
                schema: "closure",
                table: "reconciliation_lines");

            migrationBuilder.DropColumn(
                name: "value_date",
                schema: "closure",
                table: "reconciliation_lines");

            migrationBuilder.DropColumn(
                name: "decided_at",
                schema: "referral",
                table: "judicial_referrals");

            migrationBuilder.DropColumn(
                name: "decided_by_user_id",
                schema: "referral",
                table: "judicial_referrals");

            migrationBuilder.DropColumn(
                name: "external_reference_entered_at",
                schema: "referral",
                table: "judicial_referrals");

            migrationBuilder.DropColumn(
                name: "external_reference_entered_by_user_id",
                schema: "referral",
                table: "judicial_referrals");

            migrationBuilder.DropColumn(
                name: "external_reference_source",
                schema: "referral",
                table: "judicial_referrals");

            migrationBuilder.DropColumn(
                name: "notice_sent_by_user_id",
                schema: "referral",
                table: "judicial_referrals");

            migrationBuilder.DropColumn(
                name: "notice_template_code",
                schema: "referral",
                table: "judicial_referrals");

            migrationBuilder.DropColumn(
                name: "requested_at",
                schema: "referral",
                table: "judicial_referrals");

            migrationBuilder.DropColumn(
                name: "kind",
                schema: "referral",
                table: "external_status_entries");

            migrationBuilder.DropColumn(
                name: "officially_confirmed",
                schema: "referral",
                table: "external_status_entries");

            migrationBuilder.DropColumn(
                name: "source_kind",
                schema: "referral",
                table: "external_status_entries");

            migrationBuilder.DropColumn(
                name: "share_with_owner",
                schema: "closure",
                table: "closure_documents");
        }
    }
}
