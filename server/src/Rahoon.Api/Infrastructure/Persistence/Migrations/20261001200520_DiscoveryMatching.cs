using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rahoon.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DiscoveryMatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "annual_extra_payment",
                schema: "market",
                table: "opportunity_terms",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "extra_payment_recurrence",
                schema: "market",
                table: "opportunity_terms",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "next_extra_payment_date",
                schema: "market",
                table: "opportunity_terms",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "one_off_extra_payment",
                schema: "market",
                table: "opportunity_terms",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "quality",
                schema: "market",
                table: "opportunity_terms",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "schedule_known",
                schema: "market",
                table: "opportunity_terms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "developer_name",
                schema: "market",
                table: "opportunities",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "developer_party_id",
                schema: "market",
                table: "opportunities",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "preferences_revision",
                schema: "market",
                table: "buyer_requests",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "saved_searches",
                schema: "market",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    query = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: false),
                    query_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    alerts_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    alert_channel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    alerts_consent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    paused_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saved_searches", x => x.id);
                    table.ForeignKey(
                        name: "fk_saved_searches_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_saved_searches_users_applicant_user_id",
                        column: x => x.applicant_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "search_alerts",
                schema: "market",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    saved_search_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opportunity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terms_id = table.Column<Guid>(type: "uuid", nullable: false),
                    due_now = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    channel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_search_alerts", x => x.id);
                    table.ForeignKey(
                        name: "fk_search_alerts_opportunities_opportunity_id",
                        column: x => x.opportunity_id,
                        principalSchema: "market",
                        principalTable: "opportunities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_search_alerts_opportunity_terms_terms_id",
                        column: x => x.terms_id,
                        principalSchema: "market",
                        principalTable: "opportunity_terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_search_alerts_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_search_alerts_saved_searches_saved_search_id",
                        column: x => x.saved_search_id,
                        principalSchema: "market",
                        principalTable: "saved_searches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_developer_party_id",
                schema: "market",
                table: "opportunities",
                column: "developer_party_id");

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_published_point",
                schema: "market",
                table: "opportunities",
                columns: new[] { "public_latitude", "public_longitude" },
                filter: "status = 'Published'");

            migrationBuilder.CreateIndex(
                name: "ix_saved_searches_alerts_enabled_deleted_at_paused_at",
                schema: "market",
                table: "saved_searches",
                columns: new[] { "alerts_enabled", "deleted_at", "paused_at" });

            migrationBuilder.CreateIndex(
                name: "ix_saved_searches_organization_id",
                schema: "market",
                table: "saved_searches",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ux_saved_searches_one_live",
                schema: "market",
                table: "saved_searches",
                columns: new[] { "applicant_user_id", "query_hash" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_search_alerts_opportunity_id",
                schema: "market",
                table: "search_alerts",
                column: "opportunity_id");

            migrationBuilder.CreateIndex(
                name: "ix_search_alerts_organization_id",
                schema: "market",
                table: "search_alerts",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_search_alerts_status_created_at",
                schema: "market",
                table: "search_alerts",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_search_alerts_terms_id",
                schema: "market",
                table: "search_alerts",
                column: "terms_id");

            migrationBuilder.CreateIndex(
                name: "ux_search_alerts_once",
                schema: "market",
                table: "search_alerts",
                columns: new[] { "saved_search_id", "opportunity_id", "terms_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_opportunities_directory_organizations_developer_party_id",
                schema: "market",
                table: "opportunities",
                column: "developer_party_id",
                principalSchema: "directory",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Backfill the typed schedule snapshot from each version's stored inputs and result. Terms sent to an owner are
            // immutable, so nothing is recomputed: the values are read from the JSON they were computed with.
            migrationBuilder.Sql("""
                UPDATE market.opportunity_terms t SET
                    quality = t.result_json ->> 'quality',
                    extra_payment_recurrence = CASE WHEN (t.input_json -> 'developer' ->> 'extraPayment') IS NOT NULL
                        AND (t.input_json -> 'developer' ->> 'extraPaymentRecurrence') IN ('once', 'annual')
                        THEN t.input_json -> 'developer' ->> 'extraPaymentRecurrence' END,
                    next_extra_payment_date = CASE WHEN (t.input_json -> 'developer' ->> 'extraPayment') IS NOT NULL
                        THEN left(t.input_json -> 'developer' ->> 'extraPaymentDate', 10) END;
                UPDATE market.opportunity_terms t SET
                    annual_extra_payment = CASE WHEN t.extra_payment_recurrence = 'annual' THEN (t.input_json -> 'developer' ->> 'extraPayment')::numeric END,
                    one_off_extra_payment = CASE WHEN t.extra_payment_recurrence = 'once' THEN (t.input_json -> 'developer' ->> 'extraPayment')::numeric END,
                    schedule_known = (t.future_balance = 0) OR (t.installment IS NOT NULL
                        AND t.installment_frequency IN ('monthly', 'quarterly', 'semiannual', 'annual')
                        AND ((t.input_json -> 'developer' ->> 'extraPayment') IS NULL OR t.extra_payment_recurrence IS NOT NULL));
                UPDATE market.opportunities o SET
                    developer_party_id = d.party_id,
                    developer_name = left(coalesce(d.party_name, d.party_other_name), 200)
                FROM (SELECT DISTINCT ON (sale_request_id) sale_request_id, party_id, party_name, party_other_name
                      FROM market.sale_obligations WHERE kind = 'developer' AND removed_at IS NULL
                      ORDER BY sale_request_id, sort_order) d
                WHERE d.sale_request_id = o.sale_request_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_opportunities_directory_organizations_developer_party_id",
                schema: "market",
                table: "opportunities");

            migrationBuilder.DropTable(
                name: "search_alerts",
                schema: "market");

            migrationBuilder.DropTable(
                name: "saved_searches",
                schema: "market");

            migrationBuilder.DropIndex(
                name: "ix_opportunities_developer_party_id",
                schema: "market",
                table: "opportunities");

            migrationBuilder.DropIndex(
                name: "ix_opportunities_published_point",
                schema: "market",
                table: "opportunities");

            migrationBuilder.DropColumn(
                name: "annual_extra_payment",
                schema: "market",
                table: "opportunity_terms");

            migrationBuilder.DropColumn(
                name: "extra_payment_recurrence",
                schema: "market",
                table: "opportunity_terms");

            migrationBuilder.DropColumn(
                name: "next_extra_payment_date",
                schema: "market",
                table: "opportunity_terms");

            migrationBuilder.DropColumn(
                name: "one_off_extra_payment",
                schema: "market",
                table: "opportunity_terms");

            migrationBuilder.DropColumn(
                name: "quality",
                schema: "market",
                table: "opportunity_terms");

            migrationBuilder.DropColumn(
                name: "schedule_known",
                schema: "market",
                table: "opportunity_terms");

            migrationBuilder.DropColumn(
                name: "developer_name",
                schema: "market",
                table: "opportunities");

            migrationBuilder.DropColumn(
                name: "developer_party_id",
                schema: "market",
                table: "opportunities");

            migrationBuilder.DropColumn(
                name: "preferences_revision",
                schema: "market",
                table: "buyer_requests");
        }
    }
}
