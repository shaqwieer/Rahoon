using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Rahoon.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "assessment");

            migrationBuilder.EnsureSchema(
                name: "agreements");

            migrationBuilder.EnsureSchema(
                name: "comms");

            migrationBuilder.EnsureSchema(
                name: "solutions");

            migrationBuilder.EnsureSchema(
                name: "providers");

            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.EnsureSchema(
                name: "documents");

            migrationBuilder.EnsureSchema(
                name: "cases");

            migrationBuilder.EnsureSchema(
                name: "closure");

            migrationBuilder.EnsureSchema(
                name: "complaints");

            migrationBuilder.EnsureSchema(
                name: "referral");

            migrationBuilder.EnsureSchema(
                name: "admin");

            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "document_types",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    name_en = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    icon = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    validity_days = table.Column<int>(type: "integer", nullable: true),
                    sensitive = table.Column<bool>(type: "boolean", nullable: false),
                    allowed_formats = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                schema: "admin",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    endpoint = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    request_hash = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status_code = table.Column<int>(type: "integer", nullable: false),
                    response_json = table.Column<string>(type: "jsonb", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency_records", x => new { x.user_id, x.key });
                });

            migrationBuilder.CreateTable(
                name: "institution_applications",
                schema: "admin",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    org_name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    org_type = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    contact_name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    contact_email = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    contact_phone = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    job_title = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    portfolio_size = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    review_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_institution_applications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "integration_settings",
                schema: "admin",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    state = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_integration_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    short_code = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    initials = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    kind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    license_number = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    city = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    default_owner_language = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    idle_timeout_minutes = table.Column<int>(type: "integer", nullable: false),
                    mfa_required = table.Column<bool>(type: "boolean", nullable: false),
                    allowed_email_domains = table.Column<List<string>>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organizations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "otp_challenges",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    purpose = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    code_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    destination = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    context = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_otp_challenges", x => x.id);
                    table.CheckConstraint("ck_otp_attempts", "attempts >= 0 AND attempts <= 10");
                });

            migrationBuilder.CreateTable(
                name: "reference_counters",
                schema: "cases",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reference_counters", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "retention_policies",
                schema: "admin",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_category = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    period = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    basis = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    state = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_retention_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    full_name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    full_name_en = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    phone = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    password_hash = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    mfa_enrolled = table.Column<bool>(type: "boolean", nullable: false),
                    mfa_method = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    preferred_locale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    numeral_style = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    failed_login_count = table.Column<int>(type: "integer", nullable: false),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    password_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "approval_limit_policies",
                schema: "solutions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    change_summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    proposed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approval_limit_policies", x => x.id);
                    table.ForeignKey(
                        name: "fk_approval_limit_policies_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assignment_messages",
                schema: "providers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_label = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    author_side = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assignment_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_assignment_messages_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assignment_submissions",
                schema: "providers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    market_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    range_low = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    range_high = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    inspection_date = table.Column<DateOnly>(type: "date", nullable: true),
                    methodology = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    comparables_count = table.Column<int>(type: "integer", nullable: true),
                    report_document_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    checklist = table.Column<List<string>>(type: "text[]", nullable: false),
                    independence_declared = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitted_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_notes = table.Column<List<string>>(type: "text[]", nullable: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resubmit_due_on = table.Column<DateOnly>(type: "date", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assignment_submissions", x => x.id);
                    table.ForeignKey(
                        name: "fk_assignment_submissions_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cases",
                schema: "cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    status_before_pause = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: true),
                    status_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    import_batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_type = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    region = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    city = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    opened_on = table.Column<DateOnly>(type: "date", nullable: true),
                    assigned_manager_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_step = table.Column<int>(type: "integer", nullable: false),
                    duplicate_override_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    stage_due_on = table.Column<DateOnly>(type: "date", nullable: true),
                    sla_paused_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sla_paused_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    outstanding_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    outstanding_as_of = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    outstanding_source = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    arrears_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    arrears_installments = table.Column<int>(type: "integer", nullable: true),
                    arrears_since = table.Column<DateOnly>(type: "date", nullable: true),
                    pause_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cases", x => x.id);
                    table.CheckConstraint("ck_cases_draft_step", "draft_step BETWEEN 1 AND 6");
                    table.CheckConstraint("ck_cases_outstanding_nonneg", "outstanding_amount IS NULL OR outstanding_amount >= 0");
                    table.ForeignKey(
                        name: "fk_cases_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_rules",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type_key = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    required_before_status = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    uploader = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    validity_days = table.Column<int>(type: "integer", nullable: true),
                    validity_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    visible_to = table.Column<List<string>>(type: "text[]", nullable: false),
                    owner_summary_only = table.Column<bool>(type: "boolean", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_document_rules_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "download_logs",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    watermark = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_download_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_download_logs_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "import_batches",
                schema: "cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    template_version = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_rows = table.Column<int>(type: "integer", nullable: false),
                    ready_count = table.Column<int>(type: "integer", nullable: false),
                    duplicate_count = table.Column<int>(type: "integer", nullable: false),
                    error_count = table.Column<int>(type: "integer", nullable: false),
                    imported_count = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_batches", x => x.id);
                    table.ForeignKey(
                        name: "fk_import_batches_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invitations",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    full_name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    phone = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    invited_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invitations", x => x.id);
                    table.ForeignKey(
                        name: "fk_invitations_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_change_requests",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_role_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_change_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_role_change_requests_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    name_en = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                    table.ForeignKey(
                        name: "fk_roles_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "saved_views",
                schema: "cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    filters_json = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saved_views", x => x.id);
                    table.ForeignKey(
                        name: "fk_saved_views_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sla_rules",
                schema: "admin",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    business_days = table.Column<int>(type: "integer", nullable: false),
                    rule_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    pauses_on_open_complaint = table.Column<bool>(type: "boolean", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sla_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_sla_rules_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "teams",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_ar = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teams", x => x.id);
                    table.ForeignKey(
                        name: "fk_teams_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "templates",
                schema: "comms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    title = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    audience = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    body_ar = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    body_en = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    body_sms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    variables = table.Column<List<string>>(type: "text[]", nullable: false),
                    last_edited_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_templates", x => x.id);
                    table.ForeignKey(
                        name: "fk_templates_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    csrf_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    stage = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    scope = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_access_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    idle_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    absolute_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    mfa_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    step_up_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ip = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    city = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_sessions_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sessions_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "approval_limit_tiers",
                schema: "solutions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    role_key = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    level_label = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    solution_kinds = table.Column<List<string>>(type: "text[]", nullable: false),
                    solution_kinds_label = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    max_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    max_waiver_percent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    can_approve = table.Column<bool>(type: "boolean", nullable: false),
                    escalate_to_label = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approval_limit_tiers", x => x.id);
                    table.ForeignKey(
                        name: "fk_approval_limit_tiers_approval_limit_policies_policy_id",
                        column: x => x.policy_id,
                        principalSchema: "solutions",
                        principalTable: "approval_limit_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "affordability_analyses",
                schema: "assessment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    net_monthly_income = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    income_document_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    income_source_label = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    income_verified_on = table.Column<DateOnly>(type: "date", nullable: true),
                    other_obligations = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    dsr_limit = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false),
                    circumstance_indicators = table.Column<List<string>>(type: "text[]", nullable: false),
                    option_notes = table.Column<List<string>>(type: "text[]", nullable: false),
                    completed = table.Column<bool>(type: "boolean", nullable: false),
                    prepared_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_affordability_analyses", x => x.id);
                    table.ForeignKey(
                        name: "fk_affordability_analyses_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_affordability_analyses_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agreements",
                schema: "agreements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    solution_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    offer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version_label = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    rescheduled_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    waiver_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    installment_count = table.Column<int>(type: "integer", nullable: false),
                    installment_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    due_day = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    breach_missed_consecutive = table.Column<int>(type: "integer", nullable: false),
                    breach_cure_days = table.Column<int>(type: "integer", nullable: false),
                    payment_method = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    signature_method = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    consent_record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    signed_document_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    legal_review_done = table.Column<bool>(type: "boolean", nullable: false),
                    legal_reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    schedule_created = table.Column<bool>(type: "boolean", nullable: false),
                    core_system_updated = table.Column<bool>(type: "boolean", nullable: false),
                    activated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agreements", x => x.id);
                    table.ForeignKey(
                        name: "fk_agreements_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_agreements_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "appointments",
                schema: "comms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attendees = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    proposed_by = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointments", x => x.id);
                    table.ForeignKey(
                        name: "fk_appointments_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_appointments_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "approval_requests",
                schema: "solutions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_version_no = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    prepared_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitter_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    submitter_attested = table.Column<bool>(type: "boolean", nullable: false),
                    assigned_approver_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    required_tier = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    waiver_percent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    due_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    step_up_verified = table.Column<bool>(type: "boolean", nullable: false),
                    evidence = table.Column<List<string>>(type: "text[]", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approval_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_approval_requests_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_approval_requests_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assignments",
                schema: "providers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignee_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    title = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    property_label = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    due_on = table.Column<DateOnly>(type: "date", nullable: false),
                    inspection_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    inspection_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    inspection_contact = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    scope = table.Column<List<string>>(type: "text[]", nullable: false),
                    shared_document_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    fees_label = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    fee_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    access_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_assignments_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assignments_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assignments_organizations_provider_organization_id",
                        column: x => x.provider_organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_events",
                schema: "audit",
                columns: table => new
                {
                    seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    case_reference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    type = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    title = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    from_state = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    to_state = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    actor_type = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_label = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    actor_role = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    blocked = table.Column<bool>(type: "boolean", nullable: false),
                    evidence = table.Column<List<string>>(type: "text[]", nullable: false),
                    data_json = table.Column<string>(type: "jsonb", maxLength: 2000, nullable: true),
                    ip_masked = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    prev_hash = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    hash = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_events", x => x.seq);
                    table.ForeignKey(
                        name: "fk_audit_events_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_audit_events_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "breach_reviews",
                schema: "agreements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agreement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    triggered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    trigger = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    missed_installment_nos = table.Column<List<int>>(type: "integer[]", nullable: false),
                    cure_deadline = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    outcome_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    closed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_breach_reviews", x => x.id);
                    table.ForeignKey(
                        name: "fk_breach_reviews_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_breach_reviews_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "case_documents",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type_key = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    source = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    visible_to = table.Column<List<string>>(type: "text[]", nullable: false),
                    visible_to_owner = table.Column<bool>(type: "boolean", nullable: false),
                    @internal = table.Column<bool>(name: "internal", type: "boolean", nullable: false),
                    current_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version_count = table.Column<int>(type: "integer", nullable: false),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_case_documents_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_case_documents_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "case_messages",
                schema: "comms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    author_type = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    author_label = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    template_key = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    internal_only = table.Column<bool>(type: "boolean", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_by_owner_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    read_by_team_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_case_messages_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_case_messages_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "closure_documents",
                schema: "closure",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    title = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    prepared_by_dept = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    document_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    external_reference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    blocks_closure = table.Column<bool>(type: "boolean", nullable: false),
                    visible_to_owner = table.Column<bool>(type: "boolean", nullable: false),
                    prepared_on = table.Column<DateOnly>(type: "date", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_closure_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_closure_documents_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_closure_documents_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "complaints",
                schema: "complaints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    subject = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    submitted_via = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    submitted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_by_label = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    reviewer_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    due_on = table.Column<DateOnly>(type: "date", nullable: false),
                    decision = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: true),
                    response_text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    response_draft = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    owner_reaction = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    findings = table.Column<List<string>>(type: "text[]", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_complaints", x => x.id);
                    table.ForeignKey(
                        name: "fk_complaints_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_complaints_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "compliance_notices",
                schema: "solutions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    solution_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_compliance_notices", x => x.id);
                    table.ForeignKey(
                        name: "fk_compliance_notices_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_compliance_notices_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "consent_records",
                schema: "solutions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    offer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    agreement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    channel = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    otp_destination_masked = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    otp_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    device = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ip_masked = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    acknowledgements = table.Column<List<string>>(type: "text[]", nullable: false),
                    text_hash = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consent_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_consent_records_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_consent_records_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "debt_snapshots",
                schema: "cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    as_of = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sync_status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    principal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    profit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    late_fees = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    other_fees = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_debt_snapshots", x => x.id);
                    table.CheckConstraint("ck_debt_total", "total = principal + profit + late_fees + other_fees");
                    table.ForeignKey(
                        name: "fk_debt_snapshots_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_debt_snapshots_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_requests",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type_key = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    requested_from = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: true),
                    due_on = table.Column<DateOnly>(type: "date", nullable: false),
                    owner_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    channels = table.Column<List<string>>(type: "text[]", nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_document_requests_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_document_requests_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "external_status_entries",
                schema: "referral",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    referral_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    source = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    entered_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_status_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_external_status_entries_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_external_status_entries_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "financing_contracts",
                schema: "cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    contract_date = table.Column<DateOnly>(type: "date", nullable: true),
                    original_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    original_term_months = table.Column<int>(type: "integer", nullable: true),
                    remaining_term_months = table.Column<int>(type: "integer", nullable: true),
                    original_installment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    profit_type = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    first_overdue_date = table.Column<DateOnly>(type: "date", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_financing_contracts", x => x.id);
                    table.ForeignKey(
                        name: "fk_financing_contracts_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_financing_contracts_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "installment_history",
                schema: "cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    month = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    amount_due = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    amount_paid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_installment_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_installment_history_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_installment_history_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "installments",
                schema: "agreements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agreement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    no = table.Column<int>(type: "integer", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_installments", x => x.id);
                    table.CheckConstraint("ck_installment_amount", "amount > 0 AND paid_amount >= 0");
                    table.ForeignKey(
                        name: "fk_installments_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_installments_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "judicial_referrals",
                schema: "referral",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    initiated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    notice_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    objection_period_days = table.Column<int>(type: "integer", nullable: false),
                    objection_ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    evidence_pack_exported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    evidence_pack_hash = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    external_authority = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    external_request_number = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    official_status_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    official_status_source = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    official_status_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    integration_state = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_judicial_referrals", x => x.id);
                    table.ForeignKey(
                        name: "fk_judicial_referrals_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_judicial_referrals_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mortgages",
                schema: "cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mortgagee = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    registered_on = table.Column<DateOnly>(type: "date", nullable: true),
                    other_encumbrances = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    insurance_valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    deed_matched = table.Column<bool>(type: "boolean", nullable: false),
                    deed_matched_on = table.Column<DateOnly>(type: "date", nullable: true),
                    verification_source = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    legal_review_status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    legal_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    legal_reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    legal_reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mortgages", x => x.id);
                    table.ForeignKey(
                        name: "fk_mortgages_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_mortgages_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "negotiation_entries",
                schema: "solutions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    offer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    author_type = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    author_label = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    requested_due_day = table.Column<int>(type: "integer", nullable: true),
                    requested_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    requested_installment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    requested_term_months = table.Column<int>(type: "integer", nullable: true),
                    internal_only = table.Column<bool>(type: "boolean", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_negotiation_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_negotiation_entries_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_negotiation_entries_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "comms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    title = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    link = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    tone = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notifications_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "offers",
                schema: "solutions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    solution_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    sent_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decline_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_offers", x => x.id);
                    table.ForeignKey(
                        name: "fk_offers_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_offers_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "outbound_messages",
                schema: "comms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    channel = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    destination = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    template_code = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    provider = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbound_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_outbound_messages_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_outbound_messages_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "owner_accesses",
                schema: "cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    invitation_token_hash = table.Column<byte[]>(type: "bytea", nullable: true),
                    invitation_status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    invited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    invitation_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    identity_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    identity_method = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    contact_hours = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    allowed_channels = table.Column<List<string>>(type: "text[]", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_owner_accesses", x => x.id);
                    table.ForeignKey(
                        name: "fk_owner_accesses_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_owner_accesses_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "parties",
                schema: "cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    kind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    is_contract_party = table.Column<bool>(type: "boolean", nullable: false),
                    full_name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    display_name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    relation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    national_id_enc = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    national_id_masked = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    national_id_hash = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    phone_enc = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    phone_masked = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    email = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    preferred_language = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    special_needs = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    employment_status = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    contact_allowed = table.Column<bool>(type: "boolean", nullable: false),
                    identity_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    identity_verified_via = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    poa_status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parties", x => x.id);
                    table.ForeignKey(
                        name: "fk_parties_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_parties_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_records",
                schema: "agreements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    received_on = table.Column<DateOnly>(type: "date", nullable: false),
                    bank_reference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    proof_document_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    variance_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    source = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    matched_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    matched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reject_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_records", x => x.id);
                    table.CheckConstraint("ck_payment_amount", "amount > 0");
                    table.CheckConstraint("ck_payment_checker", "matched_by_user_id IS NULL OR matched_by_user_id <> recorded_by_user_id");
                    table.ForeignKey(
                        name: "fk_payment_records_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payment_records_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pii_reveal_logs",
                schema: "cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    revealed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pii_reveal_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_pii_reveal_logs_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pii_reveal_logs_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "properties",
                schema: "cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    city = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    district = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    land_area_m2 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    built_area_m2 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    year_built = table.Column<int>(type: "integer", nullable: true),
                    deed_number_enc = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    deed_number_masked = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    occupancy = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    occupancy_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    short_label = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_properties", x => x.id);
                    table.ForeignKey(
                        name: "fk_properties_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_properties_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reconciliations",
                schema: "closure",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    basis = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    expected_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    received_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    waived_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    difference = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    difference_explanation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    prepared_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reconciliations", x => x.id);
                    table.CheckConstraint("ck_recon_diff", "difference = expected_amount - received_amount - waived_amount");
                    table.ForeignKey(
                        name: "fk_reconciliations_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reconciliations_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "solution_versions",
                schema: "solutions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    outstanding_at_preparation = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    term_months = table.Column<int>(type: "integer", nullable: false),
                    first_due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    last_due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    waiver_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    waiver_percent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    down_payment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    grace_months = table.Column<int>(type: "integer", nullable: false),
                    rescheduled_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    installment_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    final_installment_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    discounted_payoff_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    dsr = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    dsr_limit = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false),
                    net_income_used = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    justification = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    breach_missed_consecutive = table.Column<int>(type: "integer", nullable: false),
                    breach_cure_days = table.Column<int>(type: "integer", nullable: false),
                    offer_validity_days = table.Column<int>(type: "integer", nullable: false),
                    prepared_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prepared_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    locked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    locked_snapshot_json = table.Column<string>(type: "jsonb", maxLength: 2000, nullable: true),
                    return_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    based_on_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_solution_versions", x => x.id);
                    table.CheckConstraint("ck_solution_term", "term_months BETWEEN 1 AND 360");
                    table.CheckConstraint("ck_solution_waiver", "waiver_amount >= 0 AND down_payment >= 0");
                    table.ForeignKey(
                        name: "fk_solution_versions_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_solution_versions_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                schema: "comms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    assignee_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assignee_role_key = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    due_on = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    kind = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    link = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tasks", x => x.id);
                    table.ForeignKey(
                        name: "fk_tasks_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tasks_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "temp_access_requests",
                schema: "admin",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    masked_case_id = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    requester_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    support_ticket_ref = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    institution_approver_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    institution_approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    auditor_approver_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    auditor_approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_temp_access_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_temp_access_requests_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_temp_access_requests_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "valuation_reports",
                schema: "assessment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    valuer_name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    market_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    range_low = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    range_high = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    methodology = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    comparables_count = table.Column<int>(type: "integer", nullable: true),
                    inspection_date = table.Column<DateOnly>(type: "date", nullable: true),
                    report_date = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: false),
                    document_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_valuation_reports", x => x.id);
                    table.CheckConstraint("ck_valuation_positive", "market_value > 0");
                    table.ForeignKey(
                        name: "fk_valuation_reports_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_valuation_reports_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "import_rows",
                schema: "cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    row_number = table.Column<int>(type: "integer", nullable: false),
                    contract_number_masked = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    raw_json = table.Column<string>(type: "jsonb", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    issues = table.Column<List<string>>(type: "text[]", nullable: false),
                    duplicate_of_case_ref = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    decision = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    decision_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_case_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_rows", x => x.id);
                    table.ForeignKey(
                        name: "fk_import_rows_import_batches_batch_id",
                        column: x => x.batch_id,
                        principalSchema: "cases",
                        principalTable: "import_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "identity",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    grant = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_permissions", x => new { x.role_id, x.permission_key });
                    table.ForeignKey(
                        name: "fk_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "memberships",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    title = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_memberships", x => x.id);
                    table.ForeignKey(
                        name: "fk_memberships_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_memberships_teams_team_id",
                        column: x => x.team_id,
                        principalSchema: "identity",
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_memberships_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_versions",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    file_name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    content_type = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_by_label = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    scan_status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    review_status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    owner_facing_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_versions", x => x.id);
                    table.CheckConstraint("ck_docver_size", "size_bytes > 0 AND size_bytes <= 20971520");
                    table.ForeignKey(
                        name: "fk_document_versions_case_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "documents",
                        principalTable: "case_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_document_versions_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "cases",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_document_versions_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reconciliation_lines",
                schema: "closure",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reconciliation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    reference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    kind = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    match_status = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reconciliation_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_reconciliation_lines_reconciliations_reconciliation_id",
                        column: x => x.reconciliation_id,
                        principalSchema: "closure",
                        principalTable: "reconciliations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "membership_roles",
                schema: "identity",
                columns: table => new
                {
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_membership_roles", x => new { x.membership_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_membership_roles_memberships_membership_id",
                        column: x => x.membership_id,
                        principalSchema: "identity",
                        principalTable: "memberships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_membership_roles_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_affordability_analyses_case_id",
                schema: "assessment",
                table: "affordability_analyses",
                column: "case_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_affordability_analyses_organization_id",
                schema: "assessment",
                table: "affordability_analyses",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_agreements_case_id",
                schema: "agreements",
                table: "agreements",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_agreements_number",
                schema: "agreements",
                table: "agreements",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_agreements_organization_id",
                schema: "agreements",
                table: "agreements",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_case_id",
                schema: "comms",
                table: "appointments",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_organization_id",
                schema: "comms",
                table: "appointments",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_limit_policies_organization_id_version_no",
                schema: "solutions",
                table: "approval_limit_policies",
                columns: new[] { "organization_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_approval_limit_tiers_policy_id",
                schema: "solutions",
                table: "approval_limit_tiers",
                column: "policy_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_requests_case_id",
                schema: "solutions",
                table: "approval_requests",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_requests_organization_id_status_assigned_approver_",
                schema: "solutions",
                table: "approval_requests",
                columns: new[] { "organization_id", "status", "assigned_approver_user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_approval_requests_subject_subject_id",
                schema: "solutions",
                table: "approval_requests",
                columns: new[] { "subject", "subject_id" },
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_assignment_messages_organization_id",
                schema: "providers",
                table: "assignment_messages",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_assignment_submissions_assignment_id_version_no",
                schema: "providers",
                table: "assignment_submissions",
                columns: new[] { "assignment_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assignment_submissions_organization_id",
                schema: "providers",
                table: "assignment_submissions",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_assignments_case_id",
                schema: "providers",
                table: "assignments",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_assignments_organization_id",
                schema: "providers",
                table: "assignments",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_assignments_provider_organization_id_status",
                schema: "providers",
                table: "assignments",
                columns: new[] { "provider_organization_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_assignments_reference",
                schema: "providers",
                table: "assignments",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_case_id",
                schema: "audit",
                table: "audit_events",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_case_id_seq",
                schema: "audit",
                table: "audit_events",
                columns: new[] { "case_id", "seq" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_id",
                schema: "audit",
                table: "audit_events",
                column: "id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_organization_id_occurred_at",
                schema: "audit",
                table: "audit_events",
                columns: new[] { "organization_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_breach_reviews_case_id",
                schema: "agreements",
                table: "breach_reviews",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_breach_reviews_organization_id",
                schema: "agreements",
                table: "breach_reviews",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_case_documents_case_id",
                schema: "documents",
                table: "case_documents",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_case_documents_organization_id",
                schema: "documents",
                table: "case_documents",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_case_messages_case_id",
                schema: "comms",
                table: "case_messages",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_case_messages_organization_id",
                schema: "comms",
                table: "case_messages",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_cases_organization_id_assigned_manager_id",
                schema: "cases",
                table: "cases",
                columns: new[] { "organization_id", "assigned_manager_id" });

            migrationBuilder.CreateIndex(
                name: "ix_cases_organization_id_stage_due_on",
                schema: "cases",
                table: "cases",
                columns: new[] { "organization_id", "stage_due_on" });

            migrationBuilder.CreateIndex(
                name: "ix_cases_organization_id_status",
                schema: "cases",
                table: "cases",
                columns: new[] { "organization_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_cases_reference",
                schema: "cases",
                table: "cases",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_closure_documents_case_id",
                schema: "closure",
                table: "closure_documents",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_closure_documents_organization_id",
                schema: "closure",
                table: "closure_documents",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_complaints_case_id",
                schema: "complaints",
                table: "complaints",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_complaints_organization_id",
                schema: "complaints",
                table: "complaints",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_complaints_reference",
                schema: "complaints",
                table: "complaints",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_compliance_notices_case_id",
                schema: "solutions",
                table: "compliance_notices",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_compliance_notices_organization_id",
                schema: "solutions",
                table: "compliance_notices",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_consent_records_case_id",
                schema: "solutions",
                table: "consent_records",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_consent_records_organization_id",
                schema: "solutions",
                table: "consent_records",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_debt_snapshots_case_id",
                schema: "cases",
                table: "debt_snapshots",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_debt_snapshots_organization_id",
                schema: "cases",
                table: "debt_snapshots",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_requests_case_id",
                schema: "documents",
                table: "document_requests",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_requests_organization_id",
                schema: "documents",
                table: "document_requests",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_rules_organization_id_document_type_key",
                schema: "documents",
                table: "document_rules",
                columns: new[] { "organization_id", "document_type_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_types_key",
                schema: "documents",
                table: "document_types",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_versions_case_id",
                schema: "documents",
                table: "document_versions",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_versions_document_id_version_no",
                schema: "documents",
                table: "document_versions",
                columns: new[] { "document_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_versions_organization_id",
                schema: "documents",
                table: "document_versions",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_download_logs_organization_id",
                schema: "documents",
                table: "download_logs",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_status_entries_case_id",
                schema: "referral",
                table: "external_status_entries",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_status_entries_organization_id",
                schema: "referral",
                table: "external_status_entries",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_financing_contracts_case_id",
                schema: "cases",
                table: "financing_contracts",
                column: "case_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financing_contracts_organization_id_contract_number",
                schema: "cases",
                table: "financing_contracts",
                columns: new[] { "organization_id", "contract_number" });

            migrationBuilder.CreateIndex(
                name: "ix_import_batches_organization_id",
                schema: "cases",
                table: "import_batches",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_rows_batch_id",
                schema: "cases",
                table: "import_rows",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_installment_history_case_id",
                schema: "cases",
                table: "installment_history",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_installment_history_case_id_month",
                schema: "cases",
                table: "installment_history",
                columns: new[] { "case_id", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_installment_history_organization_id",
                schema: "cases",
                table: "installment_history",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_installments_agreement_id_no",
                schema: "agreements",
                table: "installments",
                columns: new[] { "agreement_id", "no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_installments_case_id",
                schema: "agreements",
                table: "installments",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_installments_organization_id",
                schema: "agreements",
                table: "installments",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_institution_applications_reference",
                schema: "admin",
                table: "institution_applications",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_integration_settings_key",
                schema: "admin",
                table: "integration_settings",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invitations_organization_id",
                schema: "identity",
                table: "invitations",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_invitations_token_hash",
                schema: "identity",
                table: "invitations",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_judicial_referrals_case_id",
                schema: "referral",
                table: "judicial_referrals",
                column: "case_id",
                unique: true,
                filter: "status NOT IN ('Rejected','Withdrawn')");

            migrationBuilder.CreateIndex(
                name: "ix_judicial_referrals_organization_id",
                schema: "referral",
                table: "judicial_referrals",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_membership_roles_role_id",
                schema: "identity",
                table: "membership_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_memberships_organization_id",
                schema: "identity",
                table: "memberships",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_memberships_team_id",
                schema: "identity",
                table: "memberships",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "ix_memberships_user_id_organization_id",
                schema: "identity",
                table: "memberships",
                columns: new[] { "user_id", "organization_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mortgages_case_id",
                schema: "cases",
                table: "mortgages",
                column: "case_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mortgages_organization_id",
                schema: "cases",
                table: "mortgages",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_negotiation_entries_case_id",
                schema: "solutions",
                table: "negotiation_entries",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_negotiation_entries_organization_id",
                schema: "solutions",
                table: "negotiation_entries",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_case_id",
                schema: "comms",
                table: "notifications",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_organization_id",
                schema: "comms",
                table: "notifications",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id_read_at",
                schema: "comms",
                table: "notifications",
                columns: new[] { "user_id", "read_at" });

            migrationBuilder.CreateIndex(
                name: "ix_offers_case_id",
                schema: "solutions",
                table: "offers",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_offers_organization_id",
                schema: "solutions",
                table: "offers",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_offers_solution_version_id",
                schema: "solutions",
                table: "offers",
                column: "solution_version_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_organizations_short_code",
                schema: "identity",
                table: "organizations",
                column: "short_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_otp_challenges_session_id_purpose",
                schema: "identity",
                table: "otp_challenges",
                columns: new[] { "session_id", "purpose" });

            migrationBuilder.CreateIndex(
                name: "ix_outbound_messages_case_id",
                schema: "comms",
                table: "outbound_messages",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbound_messages_organization_id",
                schema: "comms",
                table: "outbound_messages",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_owner_accesses_case_id",
                schema: "cases",
                table: "owner_accesses",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_owner_accesses_case_id_party_id",
                schema: "cases",
                table: "owner_accesses",
                columns: new[] { "case_id", "party_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_owner_accesses_invitation_token_hash",
                schema: "cases",
                table: "owner_accesses",
                column: "invitation_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_owner_accesses_organization_id",
                schema: "cases",
                table: "owner_accesses",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_owner_accesses_user_id",
                schema: "cases",
                table: "owner_accesses",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_parties_case_id",
                schema: "cases",
                table: "parties",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_parties_organization_id_national_id_hash",
                schema: "cases",
                table: "parties",
                columns: new[] { "organization_id", "national_id_hash" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_records_case_id",
                schema: "agreements",
                table: "payment_records",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_records_organization_id_bank_reference",
                schema: "agreements",
                table: "payment_records",
                columns: new[] { "organization_id", "bank_reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pii_reveal_logs_case_id",
                schema: "cases",
                table: "pii_reveal_logs",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_pii_reveal_logs_organization_id",
                schema: "cases",
                table: "pii_reveal_logs",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_properties_case_id",
                schema: "cases",
                table: "properties",
                column: "case_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_properties_organization_id",
                schema: "cases",
                table: "properties",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_lines_reconciliation_id",
                schema: "closure",
                table: "reconciliation_lines",
                column: "reconciliation_id");

            migrationBuilder.CreateIndex(
                name: "ix_reconciliations_case_id",
                schema: "closure",
                table: "reconciliations",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_reconciliations_organization_id",
                schema: "closure",
                table: "reconciliations",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_change_requests_organization_id",
                schema: "identity",
                table: "role_change_requests",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_roles_organization_id_key",
                schema: "identity",
                table: "roles",
                columns: new[] { "organization_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_saved_views_organization_id",
                schema: "cases",
                table: "saved_views",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_sessions_organization_id",
                schema: "identity",
                table: "sessions",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_sessions_token_hash",
                schema: "identity",
                table: "sessions",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sessions_user_id",
                schema: "identity",
                table: "sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_sla_rules_organization_id_status",
                schema: "admin",
                table: "sla_rules",
                columns: new[] { "organization_id", "status" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_solution_versions_case_id",
                schema: "solutions",
                table: "solution_versions",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_solution_versions_case_id_version_no",
                schema: "solutions",
                table: "solution_versions",
                columns: new[] { "case_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_solution_versions_organization_id",
                schema: "solutions",
                table: "solution_versions",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_tasks_case_id",
                schema: "comms",
                table: "tasks",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_tasks_organization_id_assignee_user_id_status",
                schema: "comms",
                table: "tasks",
                columns: new[] { "organization_id", "assignee_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_teams_organization_id",
                schema: "identity",
                table: "teams",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_temp_access_requests_case_id",
                schema: "admin",
                table: "temp_access_requests",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_temp_access_requests_organization_id",
                schema: "admin",
                table: "temp_access_requests",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_templates_organization_id_code_version_no",
                schema: "comms",
                table: "templates",
                columns: new[] { "organization_id", "code", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                schema: "identity",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_valuation_reports_case_id",
                schema: "assessment",
                table: "valuation_reports",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_valuation_reports_organization_id",
                schema: "assessment",
                table: "valuation_reports",
                column: "organization_id");

            // Audit trail is append-only at the database level as well.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION audit.reject_audit_mutation() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'audit_events is append-only';
                END;
                $$ LANGUAGE plpgsql;
                CREATE TRIGGER trg_audit_events_append_only
                    BEFORE UPDATE OR DELETE ON audit.audit_events
                    FOR EACH ROW EXECUTE FUNCTION audit.reject_audit_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_audit_events_append_only ON audit.audit_events;
                DROP FUNCTION IF EXISTS audit.reject_audit_mutation();
                """);
            migrationBuilder.DropTable(
                name: "affordability_analyses",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "agreements",
                schema: "agreements");

            migrationBuilder.DropTable(
                name: "appointments",
                schema: "comms");

            migrationBuilder.DropTable(
                name: "approval_limit_tiers",
                schema: "solutions");

            migrationBuilder.DropTable(
                name: "approval_requests",
                schema: "solutions");

            migrationBuilder.DropTable(
                name: "assignment_messages",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "assignment_submissions",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "assignments",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "audit_events",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "breach_reviews",
                schema: "agreements");

            migrationBuilder.DropTable(
                name: "case_messages",
                schema: "comms");

            migrationBuilder.DropTable(
                name: "closure_documents",
                schema: "closure");

            migrationBuilder.DropTable(
                name: "complaints",
                schema: "complaints");

            migrationBuilder.DropTable(
                name: "compliance_notices",
                schema: "solutions");

            migrationBuilder.DropTable(
                name: "consent_records",
                schema: "solutions");

            migrationBuilder.DropTable(
                name: "debt_snapshots",
                schema: "cases");

            migrationBuilder.DropTable(
                name: "document_requests",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "document_rules",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "document_types",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "document_versions",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "download_logs",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "external_status_entries",
                schema: "referral");

            migrationBuilder.DropTable(
                name: "financing_contracts",
                schema: "cases");

            migrationBuilder.DropTable(
                name: "idempotency_records",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "import_rows",
                schema: "cases");

            migrationBuilder.DropTable(
                name: "installment_history",
                schema: "cases");

            migrationBuilder.DropTable(
                name: "installments",
                schema: "agreements");

            migrationBuilder.DropTable(
                name: "institution_applications",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "integration_settings",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "invitations",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "judicial_referrals",
                schema: "referral");

            migrationBuilder.DropTable(
                name: "membership_roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "mortgages",
                schema: "cases");

            migrationBuilder.DropTable(
                name: "negotiation_entries",
                schema: "solutions");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "comms");

            migrationBuilder.DropTable(
                name: "offers",
                schema: "solutions");

            migrationBuilder.DropTable(
                name: "otp_challenges",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "outbound_messages",
                schema: "comms");

            migrationBuilder.DropTable(
                name: "owner_accesses",
                schema: "cases");

            migrationBuilder.DropTable(
                name: "parties",
                schema: "cases");

            migrationBuilder.DropTable(
                name: "payment_records",
                schema: "agreements");

            migrationBuilder.DropTable(
                name: "pii_reveal_logs",
                schema: "cases");

            migrationBuilder.DropTable(
                name: "properties",
                schema: "cases");

            migrationBuilder.DropTable(
                name: "reconciliation_lines",
                schema: "closure");

            migrationBuilder.DropTable(
                name: "reference_counters",
                schema: "cases");

            migrationBuilder.DropTable(
                name: "retention_policies",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "role_change_requests",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "role_permissions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "saved_views",
                schema: "cases");

            migrationBuilder.DropTable(
                name: "sessions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "sla_rules",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "solution_versions",
                schema: "solutions");

            migrationBuilder.DropTable(
                name: "tasks",
                schema: "comms");

            migrationBuilder.DropTable(
                name: "temp_access_requests",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "templates",
                schema: "comms");

            migrationBuilder.DropTable(
                name: "valuation_reports",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "approval_limit_policies",
                schema: "solutions");

            migrationBuilder.DropTable(
                name: "case_documents",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "import_batches",
                schema: "cases");

            migrationBuilder.DropTable(
                name: "memberships",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "reconciliations",
                schema: "closure");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "teams",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "users",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "cases",
                schema: "cases");

            migrationBuilder.DropTable(
                name: "organizations",
                schema: "identity");
        }
    }
}
