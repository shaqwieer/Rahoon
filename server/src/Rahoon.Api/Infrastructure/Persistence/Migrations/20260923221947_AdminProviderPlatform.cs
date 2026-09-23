using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rahoon.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminProviderPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "closed_at",
                schema: "admin",
                table: "temp_access_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "decision_note",
                schema: "admin",
                table: "temp_access_requests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "institution_notified_at",
                schema: "admin",
                table: "temp_access_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "rejected_by_user_id",
                schema: "admin",
                table: "temp_access_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "after_action",
                schema: "admin",
                table: "retention_policies",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "legal_reference",
                schema: "admin",
                table: "retention_policies",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                schema: "admin",
                table: "retention_policies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_user_id",
                schema: "admin",
                table: "retention_policies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "consent_at",
                schema: "admin",
                table: "institution_applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "consent_policy_version",
                schema: "admin",
                table: "institution_applications",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "created_organization_id",
                schema: "admin",
                table: "institution_applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "decided_at",
                schema: "admin",
                table: "institution_applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "verification_note",
                schema: "admin",
                table: "institution_applications",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "job_heartbeats",
                schema: "admin",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    last_run_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_result = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_heartbeats", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "platform_default_rules",
                schema: "admin",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    minimum_label = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    value = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    editable = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_default_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "temp_access_view_logs",
                schema: "admin",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    screen = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_temp_access_view_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_temp_access_view_logs_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_temp_access_view_logs_temp_access_requests_request_id",
                        column: x => x.request_id,
                        principalSchema: "admin",
                        principalTable: "temp_access_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_platform_default_rules_key",
                schema: "admin",
                table: "platform_default_rules",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_temp_access_view_logs_organization_id",
                schema: "admin",
                table: "temp_access_view_logs",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_temp_access_view_logs_request_id",
                schema: "admin",
                table: "temp_access_view_logs",
                column: "request_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_heartbeats",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "platform_default_rules",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "temp_access_view_logs",
                schema: "admin");

            migrationBuilder.DropColumn(
                name: "closed_at",
                schema: "admin",
                table: "temp_access_requests");

            migrationBuilder.DropColumn(
                name: "decision_note",
                schema: "admin",
                table: "temp_access_requests");

            migrationBuilder.DropColumn(
                name: "institution_notified_at",
                schema: "admin",
                table: "temp_access_requests");

            migrationBuilder.DropColumn(
                name: "rejected_by_user_id",
                schema: "admin",
                table: "temp_access_requests");

            migrationBuilder.DropColumn(
                name: "after_action",
                schema: "admin",
                table: "retention_policies");

            migrationBuilder.DropColumn(
                name: "legal_reference",
                schema: "admin",
                table: "retention_policies");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "admin",
                table: "retention_policies");

            migrationBuilder.DropColumn(
                name: "updated_by_user_id",
                schema: "admin",
                table: "retention_policies");

            migrationBuilder.DropColumn(
                name: "consent_at",
                schema: "admin",
                table: "institution_applications");

            migrationBuilder.DropColumn(
                name: "consent_policy_version",
                schema: "admin",
                table: "institution_applications");

            migrationBuilder.DropColumn(
                name: "created_organization_id",
                schema: "admin",
                table: "institution_applications");

            migrationBuilder.DropColumn(
                name: "decided_at",
                schema: "admin",
                table: "institution_applications");

            migrationBuilder.DropColumn(
                name: "verification_note",
                schema: "admin",
                table: "institution_applications");
        }
    }
}
