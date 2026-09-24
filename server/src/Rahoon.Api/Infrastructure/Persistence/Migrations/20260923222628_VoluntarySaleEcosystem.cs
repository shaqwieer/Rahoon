using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rahoon.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VoluntarySaleEcosystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ecosystem");

            migrationBuilder.EnsureSchema(
                name: "sale");

            migrationBuilder.CreateTable(
                name: "billing_plans",
                schema: "ecosystem",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    monthly_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    active_case_limit = table.Column<int>(type: "integer", nullable: true),
                    limit_label = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_billing_plans", x => x.id);
                    table.CheckConstraint("ck_plan_price", "monthly_price IS NULL OR monthly_price >= 0");
                });

            migrationBuilder.CreateTable(
                name: "institution_integrations",
                schema: "ecosystem",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution_organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capability = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    provider_label = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    mode = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    health = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    last_update_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_institution_integrations", x => x.id);
                    table.CheckConstraint("ck_institution_integration_mode", "mode IN ('enabled','simulated','disabled')");
                    table.ForeignKey(
                        name: "fk_institution_integrations_organizations_institution_organiza",
                        column: x => x.institution_organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "institution_providers",
                schema: "ecosystem",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_type = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    framework_fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    commission_rate = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    added_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_institution_providers", x => x.id);
                    table.CheckConstraint("ck_institution_provider_rate", "commission_rate IS NULL OR (commission_rate >= 0 AND commission_rate < 0.2)");
                    table.ForeignKey(
                        name: "fk_institution_providers_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_institution_providers_organizations_provider_organization_id",
                        column: x => x.provider_organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "institution_subscriptions",
                schema: "ecosystem",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution_organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_key = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    trial = table.Column<bool>(type: "boolean", nullable: false),
                    started_on = table.Column<DateOnly>(type: "date", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_institution_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_institution_subscriptions_organizations_institution_organiz",
                        column: x => x.institution_organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "integration_attempts",
                schema: "ecosystem",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution_organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    capability = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    state = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    subject_reference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_integration_attempts", x => x.id);
                    table.ForeignKey(
                        name: "fk_integration_attempts_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_integration_attempts_organizations_institution_organization",
                        column: x => x.institution_organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "platform_invoices",
                schema: "ecosystem",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    institution_organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_key = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    period = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    active_cases = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    issued_on = table.Column<DateOnly>(type: "date", nullable: false),
                    due_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    payment_reference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_invoices", x => x.id);
                    table.CheckConstraint("ck_platform_invoice_amount", "amount >= 0");
                    table.ForeignKey(
                        name: "fk_platform_invoices_organizations_institution_organization_id",
                        column: x => x.institution_organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "provider_invoices",
                schema: "ecosystem",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_reference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    lender_organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    issued_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    payment_reference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    paid_recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provider_invoices", x => x.id);
                    table.CheckConstraint("ck_provider_invoice_amount", "amount > 0");
                    table.CheckConstraint("ck_provider_invoice_checker", "decided_by_user_id IS NULL OR submitted_by_user_id IS NULL OR decided_by_user_id <> submitted_by_user_id");
                    table.ForeignKey(
                        name: "fk_provider_invoices_assignments_assignment_id",
                        column: x => x.assignment_id,
                        principalSchema: "providers",
                        principalTable: "assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_provider_invoices_organizations_lender_organization_id",
                        column: x => x.lender_organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_provider_invoices_organizations_provider_organization_id",
                        column: x => x.provider_organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "provider_profiles",
                schema: "ecosystem",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_ref = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    provider_type = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    city = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    cr_number = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    current_step = table.Column<int>(type: "integer", nullable: false),
                    completed_steps = table.Column<List<int>>(type: "integer[]", nullable: false),
                    step_data_json = table.Column<string>(type: "jsonb", maxLength: 2000, nullable: false),
                    last_saved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    representative_name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    team_count = table.Column<int>(type: "integer", nullable: false),
                    team_individually_licensed = table.Column<bool>(type: "boolean", nullable: false),
                    billing_iban_masked = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    independence_declared = table.Column<bool>(type: "boolean", nullable: false),
                    data_protection_signed = table.Column<bool>(type: "boolean", nullable: false),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    suspended_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provider_profiles", x => x.id);
                    table.CheckConstraint("ck_provider_step", "current_step BETWEEN 1 AND 6");
                    table.ForeignKey(
                        name: "fk_provider_profiles_organizations_provider_organization_id",
                        column: x => x.provider_organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "report_schedules",
                schema: "ecosystem",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    metric = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    breakdown = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    period_from = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    period_to = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    frequency = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    recipient_user_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_schedules", x => x.id);
                    table.ForeignKey(
                        name: "fk_report_schedules_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales",
                schema: "sale",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    buyer_reference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    request_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    request_channel = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    request_consent_record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    proposed_min_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    decision_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    decision_prepared_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decision_submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_approval_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decision_approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valuation_report_id = table.Column<Guid>(type: "uuid", nullable: true),
                    valuation_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    valuation_date = table.Column<DateOnly>(type: "date", nullable: true),
                    valuer_name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    outstanding_debt = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    debt_as_of = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    broker_rate = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    other_fees_estimate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    listing_status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    area_label = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    asking_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    evacuation_days = table.Column<int>(type: "integer", nullable: false),
                    visit_terms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    occupancy_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    approved_photo_count = table.Column<int>(type: "integer", nullable: false),
                    listing_prepared_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    listing_reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    listing_reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    broker_assignment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    broker_organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    accepted_offer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    close_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales", x => x.id);
                    table.CheckConstraint("ck_sale_broker_rate", "broker_rate >= 0 AND broker_rate < 0.2");
                    table.CheckConstraint("ck_sale_fees", "other_fees_estimate >= 0");
                    table.ForeignKey(
                        name: "fk_sales_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workflow_versions",
                schema: "ecosystem",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution_organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: true),
                    stages_json = table.Column<string>(type: "jsonb", maxLength: 2000, nullable: false),
                    change_summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    based_on_version_no = table.Column<int>(type: "integer", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approval_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_versions", x => x.id);
                    table.CheckConstraint("ck_workflow_second_approver", "approved_by_user_id IS NULL OR submitted_by_user_id IS NULL OR approved_by_user_id <> submitted_by_user_id");
                    table.ForeignKey(
                        name: "fk_workflow_versions_organizations_institution_organization_id",
                        column: x => x.institution_organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "provider_licenses",
                schema: "ecosystem",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    number = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    file_name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    storage_key = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    sha256 = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    content_type = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provider_licenses", x => x.id);
                    table.ForeignKey(
                        name: "fk_provider_licenses_provider_profile_provider_profile_id",
                        column: x => x.provider_profile_id,
                        principalSchema: "ecosystem",
                        principalTable: "provider_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "provider_review_decisions",
                schema: "ecosystem",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    open_items = table.Column<List<string>>(type: "text[]", nullable: false),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provider_review_decisions", x => x.id);
                    table.ForeignKey(
                        name: "fk_provider_review_decisions_provider_profiles_provider_profil",
                        column: x => x.provider_profile_id,
                        principalSchema: "ecosystem",
                        principalTable: "provider_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "buyer_offers",
                schema: "sale",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    payment_method = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    proof_of_funds = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    conditions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    proposed_transfer_days = table.Column<int>(type: "integer", nullable: false),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: false),
                    certainty = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    buyer_label = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    buyer_nda_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    entered_by_side = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    entered_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    shared_with_owner_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    owner_decision_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    owner_consent_record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_decline_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    recommendation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    approval_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_buyer_offers", x => x.id);
                    table.CheckConstraint("ck_buyer_offer_days", "proposed_transfer_days BETWEEN 1 AND 365");
                    table.CheckConstraint("ck_buyer_offer_price", "price > 0");
                    table.ForeignKey(
                        name: "fk_buyer_offers_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_buyer_offers_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_buyer_offers_voluntary_sale_sale_id",
                        column: x => x.sale_id,
                        principalSchema: "sale",
                        principalTable: "sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "consents",
                schema: "sale",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    min_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    mandate_days = table.Column<int>(type: "integer", nullable: false),
                    mandate_start = table.Column<DateOnly>(type: "date", nullable: false),
                    mandate_end = table.Column<DateOnly>(type: "date", nullable: false),
                    visit_days = table.Column<List<string>>(type: "text[]", nullable: false),
                    visit_window = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    consent_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    signed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    text_version = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    text_hash = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    withdrawn_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    withdraw_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    fulfilled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consents", x => x.id);
                    table.CheckConstraint("ck_sale_consent_mandate", "mandate_end > mandate_start");
                    table.CheckConstraint("ck_sale_consent_min", "min_price > 0");
                    table.ForeignKey(
                        name: "fk_consents_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_consents_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_consents_voluntary_sale_sale_id",
                        column: x => x.sale_id,
                        principalSchema: "sale",
                        principalTable: "sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prep_items",
                schema: "sale",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    title = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    memo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    scheduled_on = table.Column<DateOnly>(type: "date", nullable: true),
                    responsible = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prep_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_prep_items_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prep_items_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prep_items_voluntary_sale_sale_id",
                        column: x => x.sale_id,
                        principalSchema: "sale",
                        principalTable: "sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tracking_steps",
                schema: "sale",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    title = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    memo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    source = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    actual_date = table.Column<DateOnly>(type: "date", nullable: true),
                    expected_date = table.Column<DateOnly>(type: "date", nullable: true),
                    reference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    document_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entered_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tracking_steps", x => x.id);
                    table.ForeignKey(
                        name: "fk_tracking_steps_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tracking_steps_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tracking_steps_voluntary_sale_sale_id",
                        column: x => x.sale_id,
                        principalSchema: "sale",
                        principalTable: "sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_billing_plans_key",
                schema: "ecosystem",
                table: "billing_plans",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_buyer_offers_case_id",
                schema: "sale",
                table: "buyer_offers",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_buyer_offers_organization_id",
                schema: "sale",
                table: "buyer_offers",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_buyer_offers_sale_id_code",
                schema: "sale",
                table: "buyer_offers",
                columns: new[] { "sale_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_consents_case_id",
                schema: "sale",
                table: "consents",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_consents_organization_id",
                schema: "sale",
                table: "consents",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_consents_sale_id_version_no",
                schema: "sale",
                table: "consents",
                columns: new[] { "sale_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_institution_integrations_institution_organization_id_capabi",
                schema: "ecosystem",
                table: "institution_integrations",
                columns: new[] { "institution_organization_id", "capability" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_institution_providers_organization_id_provider_organization",
                schema: "ecosystem",
                table: "institution_providers",
                columns: new[] { "organization_id", "provider_organization_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_institution_providers_provider_organization_id",
                schema: "ecosystem",
                table: "institution_providers",
                column: "provider_organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_institution_subscriptions_institution_organization_id",
                schema: "ecosystem",
                table: "institution_subscriptions",
                column: "institution_organization_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_integration_attempts_case_id",
                schema: "ecosystem",
                table: "integration_attempts",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_integration_attempts_institution_organization_id_capability",
                schema: "ecosystem",
                table: "integration_attempts",
                columns: new[] { "institution_organization_id", "capability", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_platform_invoices_institution_organization_id",
                schema: "ecosystem",
                table: "platform_invoices",
                column: "institution_organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_platform_invoices_number",
                schema: "ecosystem",
                table: "platform_invoices",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_prep_items_case_id",
                schema: "sale",
                table: "prep_items",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_prep_items_organization_id",
                schema: "sale",
                table: "prep_items",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_prep_items_sale_id_key",
                schema: "sale",
                table: "prep_items",
                columns: new[] { "sale_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_provider_invoices_assignment_id",
                schema: "ecosystem",
                table: "provider_invoices",
                column: "assignment_id",
                unique: true,
                filter: "status <> 'Rejected'");

            migrationBuilder.CreateIndex(
                name: "ix_provider_invoices_lender_organization_id_status",
                schema: "ecosystem",
                table: "provider_invoices",
                columns: new[] { "lender_organization_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_provider_invoices_number",
                schema: "ecosystem",
                table: "provider_invoices",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_provider_invoices_provider_organization_id_status",
                schema: "ecosystem",
                table: "provider_invoices",
                columns: new[] { "provider_organization_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_provider_licenses_provider_profile_id_kind",
                schema: "ecosystem",
                table: "provider_licenses",
                columns: new[] { "provider_profile_id", "kind" },
                unique: true,
                filter: "is_current");

            migrationBuilder.CreateIndex(
                name: "ix_provider_profiles_application_ref",
                schema: "ecosystem",
                table: "provider_profiles",
                column: "application_ref",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_provider_profiles_provider_organization_id",
                schema: "ecosystem",
                table: "provider_profiles",
                column: "provider_organization_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_provider_review_decisions_provider_profile_id",
                schema: "ecosystem",
                table: "provider_review_decisions",
                column: "provider_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_schedules_organization_id",
                schema: "ecosystem",
                table: "report_schedules",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_buyer_reference",
                schema: "sale",
                table: "sales",
                column: "buyer_reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_organization_id",
                schema: "sale",
                table: "sales",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ux_sales_open_per_case",
                schema: "sale",
                table: "sales",
                column: "case_id",
                unique: true,
                filter: "status IN ('Requested','PendingDecision','AwaitingConsent','Active','OfferApproved')");

            migrationBuilder.CreateIndex(
                name: "ix_tracking_steps_case_id",
                schema: "sale",
                table: "tracking_steps",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_tracking_steps_organization_id",
                schema: "sale",
                table: "tracking_steps",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_tracking_steps_sale_id_key",
                schema: "sale",
                table: "tracking_steps",
                columns: new[] { "sale_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_versions_institution_organization_id_version_no",
                schema: "ecosystem",
                table: "workflow_versions",
                columns: new[] { "institution_organization_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_workflow_one_open_draft",
                schema: "ecosystem",
                table: "workflow_versions",
                column: "institution_organization_id",
                unique: true,
                filter: "status IN ('Draft','PendingApproval')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "billing_plans",
                schema: "ecosystem");

            migrationBuilder.DropTable(
                name: "buyer_offers",
                schema: "sale");

            migrationBuilder.DropTable(
                name: "consents",
                schema: "sale");

            migrationBuilder.DropTable(
                name: "institution_integrations",
                schema: "ecosystem");

            migrationBuilder.DropTable(
                name: "institution_providers",
                schema: "ecosystem");

            migrationBuilder.DropTable(
                name: "institution_subscriptions",
                schema: "ecosystem");

            migrationBuilder.DropTable(
                name: "integration_attempts",
                schema: "ecosystem");

            migrationBuilder.DropTable(
                name: "platform_invoices",
                schema: "ecosystem");

            migrationBuilder.DropTable(
                name: "prep_items",
                schema: "sale");

            migrationBuilder.DropTable(
                name: "provider_invoices",
                schema: "ecosystem");

            migrationBuilder.DropTable(
                name: "provider_licenses",
                schema: "ecosystem");

            migrationBuilder.DropTable(
                name: "provider_review_decisions",
                schema: "ecosystem");

            migrationBuilder.DropTable(
                name: "report_schedules",
                schema: "ecosystem");

            migrationBuilder.DropTable(
                name: "tracking_steps",
                schema: "sale");

            migrationBuilder.DropTable(
                name: "workflow_versions",
                schema: "ecosystem");

            migrationBuilder.DropTable(
                name: "provider_profiles",
                schema: "ecosystem");

            migrationBuilder.DropTable(
                name: "sales",
                schema: "sale");
        }
    }
}
