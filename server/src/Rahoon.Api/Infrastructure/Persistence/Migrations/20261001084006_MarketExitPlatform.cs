using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rahoon.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MarketExitPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "market");

            migrationBuilder.AlterColumn<string>(
                name: "national_id_masked",
                schema: "identity",
                table: "individual_profiles",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "national_id_hash",
                schema: "identity",
                table: "individual_profiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "national_id_enc",
                schema: "identity",
                table: "individual_profiles",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "id_type",
                schema: "identity",
                table: "individual_profiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.CreateTable(
                name: "buyer_requests",
                schema: "market",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_draft_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    status_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    available_now = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    installment_comfort = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    installment_frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    max_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    purchase_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    cities = table.Column<List<string>>(type: "text[]", nullable: false),
                    areas_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    property_types = table.Column<List<string>>(type: "text[]", nullable: false),
                    area_min = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    area_max = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    bedrooms_min = table.Column<int>(type: "integer", nullable: true),
                    readiness = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    delivery_by = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    contact_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    declarations_accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    declarations_version = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    reviewed_available_now = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    capacity_review_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    capacity_reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    capacity_reviewed_by_label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    finance_approval_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    finance_approval_source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    finance_approval_date = table.Column<DateOnly>(type: "date", nullable: true),
                    finance_approval_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    assigned_to_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_to_label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    decision_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    withdraw_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_demo = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_buyer_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_buyer_requests_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_buyer_requests_users_applicant_user_id",
                        column: x => x.applicant_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "completion_requests",
                schema: "market",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    items = table.Column<List<string>>(type: "text[]", nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by_label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    answered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_completion_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_completion_requests_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contact_messages",
                schema: "market",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    phone_enc = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    phone_masked = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    topic = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    handled_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    handled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    handled_by_label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    handled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contact_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_contact_messages_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "events",
                schema: "market",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    from_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    to_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    actor_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    visible_to_applicant = table.Column<bool>(type: "boolean", nullable: false),
                    data_json = table.Column<string>(type: "jsonb", maxLength: 2000, nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_events_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_log",
                schema: "market",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    destination_masked = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    body = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_log", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_log_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "obligation_parties",
                schema: "market",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_demo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_obligation_parties", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sale_requests",
                schema: "market",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_draft_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    status_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    property_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    city = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    district = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    project = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    obligation_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    answers = table.Column<string>(type: "jsonb", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    location_display_wish = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    location_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    contact_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    contact_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    relationship_declared = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    declarations_accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    declarations_version = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    assigned_to_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_to_label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_marked_complete_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    withdraw_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_demo = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sale_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_sale_requests_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sale_requests_users_applicant_user_id",
                        column: x => x.applicant_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "figure_verifications",
                schema: "market",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    source_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_date = table.Column<DateOnly>(type: "date", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    verified_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    verified_by_label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    superseded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_figure_verifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_figure_verifications_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_figure_verifications_sale_requests_sale_request_id",
                        column: x => x.sale_request_id,
                        principalSchema: "market",
                        principalTable: "sale_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "listing_photos",
                schema: "market",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_cover = table.Column<bool>(type: "boolean", nullable: false),
                    review_status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    review_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_listing_photos", x => x.id);
                    table.ForeignKey(
                        name: "fk_listing_photos_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_listing_photos_sale_requests_sale_request_id",
                        column: x => x.sale_request_id,
                        principalSchema: "market",
                        principalTable: "sale_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "opportunities",
                schema: "market",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sale_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    status_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: true),
                    property_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    city = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    district = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    project = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    track = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    area = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    bedrooms = table.Column<int>(type: "integer", nullable: true),
                    bathrooms = table.Column<int>(type: "integer", nullable: true),
                    readiness = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    delivery_month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    specs = table.Column<string>(type: "jsonb", nullable: false),
                    features = table.Column<List<string>>(type: "text[]", nullable: false),
                    exact_latitude = table.Column<double>(type: "double precision", nullable: true),
                    exact_longitude = table.Column<double>(type: "double precision", nullable: true),
                    location_precision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    public_latitude = table.Column<double>(type: "double precision", nullable: true),
                    public_longitude = table.Column<double>(type: "double precision", nullable: true),
                    photo_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    checklist = table.Column<List<string>>(type: "text[]", nullable: false),
                    draft_terms_id = table.Column<Guid>(type: "uuid", nullable: true),
                    published_terms_id = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    first_published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    pause_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    withdraw_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    prepared_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prepared_by_label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    assigned_to_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_to_label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    is_demo = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opportunities", x => x.id);
                    table.ForeignKey(
                        name: "fk_opportunities_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_opportunities_sale_requests_sale_request_id",
                        column: x => x.sale_request_id,
                        principalSchema: "market",
                        principalTable: "sale_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "private_documents",
                schema: "market",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    obligation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    review_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_private_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_private_documents_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_private_documents_sale_requests_sale_request_id",
                        column: x => x.sale_request_id,
                        principalSchema: "market",
                        principalTable: "sale_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sale_obligations",
                schema: "market",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: true),
                    party_other_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    relation_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    answers = table.Column<string>(type: "jsonb", nullable: false),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sale_obligations", x => x.id);
                    table.ForeignKey(
                        name: "fk_sale_obligations_obligation_parties_party_id",
                        column: x => x.party_id,
                        principalSchema: "market",
                        principalTable: "obligation_parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sale_obligations_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sale_obligations_sale_requests_sale_request_id",
                        column: x => x.sale_request_id,
                        principalSchema: "market",
                        principalTable: "sale_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "opportunity_terms",
                schema: "market",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    opportunity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    track = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    input_json = table.Column<string>(type: "jsonb", maxLength: 2000, nullable: false),
                    result_json = table.Column<string>(type: "jsonb", maxLength: 2000, nullable: false),
                    due_now = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    purchase_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    future_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    installment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    installment_frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    installment_monthly_equivalent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    largest_extra_payment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    remaining_months = table.Column<int>(type: "integer", nullable: true),
                    needs_new_financing = table.Column<bool>(type: "boolean", nullable: false),
                    complete = table.Column<bool>(type: "boolean", nullable: false),
                    transfer_conditions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    verification_scope = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    verified_on = table.Column<DateOnly>(type: "date", nullable: true),
                    prepared_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prepared_by_label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    sent_to_owner_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    owner_decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    owner_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    owner_confirmation_text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opportunity_terms", x => x.id);
                    table.ForeignKey(
                        name: "fk_opportunity_terms_opportunities_opportunity_id",
                        column: x => x.opportunity_id,
                        principalSchema: "market",
                        principalTable: "opportunities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_opportunity_terms_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "saved_opportunities",
                schema: "market",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opportunity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saved_opportunities", x => x.id);
                    table.ForeignKey(
                        name: "fk_saved_opportunities_opportunities_opportunity_id",
                        column: x => x.opportunity_id,
                        principalSchema: "market",
                        principalTable: "opportunities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_saved_opportunities_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "external_approvals",
                schema: "market",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    obligation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    conditions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decision_date = table.Column<DateOnly>(type: "date", nullable: true),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_by_label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_approvals", x => x.id);
                    table.ForeignKey(
                        name: "fk_external_approvals_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_external_approvals_sale_obligations_obligation_id",
                        column: x => x.obligation_id,
                        principalSchema: "market",
                        principalTable: "sale_obligations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_external_approvals_sale_requests_sale_request_id",
                        column: x => x.sale_request_id,
                        principalSchema: "market",
                        principalTable: "sale_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "interests",
                schema: "market",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opportunity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terms_id = table.Column<Guid>(type: "uuid", nullable: false),
                    buyer_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    contact_preference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    contact_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    status_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    assigned_to_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_to_label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    close_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_interests", x => x.id);
                    table.ForeignKey(
                        name: "fk_interests_buyer_requests_buyer_request_id",
                        column: x => x.buyer_request_id,
                        principalSchema: "market",
                        principalTable: "buyer_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_interests_opportunities_opportunity_id",
                        column: x => x.opportunity_id,
                        principalSchema: "market",
                        principalTable: "opportunities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_interests_opportunity_terms_terms_id",
                        column: x => x.terms_id,
                        principalSchema: "market",
                        principalTable: "opportunity_terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_interests_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_buyer_requests_applicant_user_id_client_draft_id",
                schema: "market",
                table: "buyer_requests",
                columns: new[] { "applicant_user_id", "client_draft_id" },
                unique: true,
                filter: "client_draft_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_buyer_requests_organization_id_status",
                schema: "market",
                table: "buyer_requests",
                columns: new[] { "organization_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_buyer_requests_reference",
                schema: "market",
                table: "buyer_requests",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_buyer_requests_one_live",
                schema: "market",
                table: "buyer_requests",
                column: "applicant_user_id",
                unique: true,
                filter: "status NOT IN ('Withdrawn', 'Rejected')");

            migrationBuilder.CreateIndex(
                name: "ix_completion_requests_organization_id",
                schema: "market",
                table: "completion_requests",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_completion_requests_subject_id_requested_at",
                schema: "market",
                table: "completion_requests",
                columns: new[] { "subject_id", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "ix_contact_messages_organization_id",
                schema: "market",
                table: "contact_messages",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_contact_messages_reference",
                schema: "market",
                table: "contact_messages",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_contact_messages_status_created_at",
                schema: "market",
                table: "contact_messages",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_events_applicant_user_id_at",
                schema: "market",
                table: "events",
                columns: new[] { "applicant_user_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_events_organization_id",
                schema: "market",
                table: "events",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_events_subject_id_at",
                schema: "market",
                table: "events",
                columns: new[] { "subject_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_external_approvals_obligation_id_recorded_at",
                schema: "market",
                table: "external_approvals",
                columns: new[] { "obligation_id", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "ix_external_approvals_organization_id",
                schema: "market",
                table: "external_approvals",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_approvals_sale_request_id",
                schema: "market",
                table: "external_approvals",
                column: "sale_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_figure_verifications_organization_id",
                schema: "market",
                table: "figure_verifications",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_figure_verifications_sale_request_id_field_key",
                schema: "market",
                table: "figure_verifications",
                columns: new[] { "sale_request_id", "field_key" });

            migrationBuilder.CreateIndex(
                name: "ix_interests_buyer_request_id",
                schema: "market",
                table: "interests",
                column: "buyer_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_interests_opportunity_id_status",
                schema: "market",
                table: "interests",
                columns: new[] { "opportunity_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_interests_organization_id",
                schema: "market",
                table: "interests",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_interests_reference",
                schema: "market",
                table: "interests",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_interests_terms_id",
                schema: "market",
                table: "interests",
                column: "terms_id");

            migrationBuilder.CreateIndex(
                name: "ux_interests_one_open",
                schema: "market",
                table: "interests",
                columns: new[] { "applicant_user_id", "opportunity_id" },
                unique: true,
                filter: "status <> 'Withdrawn'");

            migrationBuilder.CreateIndex(
                name: "ix_listing_photos_organization_id",
                schema: "market",
                table: "listing_photos",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_listing_photos_sale_request_id",
                schema: "market",
                table: "listing_photos",
                column: "sale_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_log_organization_id",
                schema: "market",
                table: "notification_log",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_log_user_id_at",
                schema: "market",
                table: "notification_log",
                columns: new[] { "user_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_obligation_parties_kind_name_ar",
                schema: "market",
                table: "obligation_parties",
                columns: new[] { "kind", "name_ar" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_organization_id",
                schema: "market",
                table: "opportunities",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_reference",
                schema: "market",
                table: "opportunities",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_sale_request_id",
                schema: "market",
                table: "opportunities",
                column: "sale_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_status_city_property_type",
                schema: "market",
                table: "opportunities",
                columns: new[] { "status", "city", "property_type" });

            migrationBuilder.CreateIndex(
                name: "ix_opportunity_terms_opportunity_id_version_no",
                schema: "market",
                table: "opportunity_terms",
                columns: new[] { "opportunity_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_opportunity_terms_organization_id",
                schema: "market",
                table: "opportunity_terms",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_private_documents_organization_id",
                schema: "market",
                table: "private_documents",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_private_documents_sale_request_id",
                schema: "market",
                table: "private_documents",
                column: "sale_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_obligations_organization_id",
                schema: "market",
                table: "sale_obligations",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_obligations_party_id",
                schema: "market",
                table: "sale_obligations",
                column: "party_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_obligations_sale_request_id",
                schema: "market",
                table: "sale_obligations",
                column: "sale_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_requests_applicant_user_id_client_draft_id",
                schema: "market",
                table: "sale_requests",
                columns: new[] { "applicant_user_id", "client_draft_id" },
                unique: true,
                filter: "client_draft_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sale_requests_organization_id_status",
                schema: "market",
                table: "sale_requests",
                columns: new[] { "organization_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_sale_requests_reference",
                schema: "market",
                table: "sale_requests",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_saved_opportunities_applicant_user_id_opportunity_id",
                schema: "market",
                table: "saved_opportunities",
                columns: new[] { "applicant_user_id", "opportunity_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_saved_opportunities_opportunity_id",
                schema: "market",
                table: "saved_opportunities",
                column: "opportunity_id");

            migrationBuilder.CreateIndex(
                name: "ix_saved_opportunities_organization_id",
                schema: "market",
                table: "saved_opportunities",
                column: "organization_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "completion_requests",
                schema: "market");

            migrationBuilder.DropTable(
                name: "contact_messages",
                schema: "market");

            migrationBuilder.DropTable(
                name: "events",
                schema: "market");

            migrationBuilder.DropTable(
                name: "external_approvals",
                schema: "market");

            migrationBuilder.DropTable(
                name: "figure_verifications",
                schema: "market");

            migrationBuilder.DropTable(
                name: "interests",
                schema: "market");

            migrationBuilder.DropTable(
                name: "listing_photos",
                schema: "market");

            migrationBuilder.DropTable(
                name: "notification_log",
                schema: "market");

            migrationBuilder.DropTable(
                name: "private_documents",
                schema: "market");

            migrationBuilder.DropTable(
                name: "saved_opportunities",
                schema: "market");

            migrationBuilder.DropTable(
                name: "sale_obligations",
                schema: "market");

            migrationBuilder.DropTable(
                name: "buyer_requests",
                schema: "market");

            migrationBuilder.DropTable(
                name: "opportunity_terms",
                schema: "market");

            migrationBuilder.DropTable(
                name: "obligation_parties",
                schema: "market");

            migrationBuilder.DropTable(
                name: "opportunities",
                schema: "market");

            migrationBuilder.DropTable(
                name: "sale_requests",
                schema: "market");

            migrationBuilder.AlterColumn<string>(
                name: "national_id_masked",
                schema: "identity",
                table: "individual_profiles",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "national_id_hash",
                schema: "identity",
                table: "individual_profiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "national_id_enc",
                schema: "identity",
                table: "individual_profiles",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "id_type",
                schema: "identity",
                table: "individual_profiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);
        }
    }
}
