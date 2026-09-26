using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rahoon.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RahoonTeamWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "assigned_at",
                schema: "requests",
                table: "requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "identity_check_note",
                schema: "requests",
                table: "requests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "identity_checked_at",
                schema: "requests",
                table: "requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "identity_checked_by_user_id",
                schema: "requests",
                table: "requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "coordination_entries",
                schema: "requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    counterpart = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    evidence_document_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    visible_to_applicant = table.Column<bool>(type: "boolean", nullable: false),
                    applicant_text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    corrects_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_by_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_coordination_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_coordination_entries_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_coordination_entries_requests_request_id",
                        column: x => x.request_id,
                        principalSchema: "requests",
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "request_messages",
                schema: "requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    is_internal = table.Column<bool>(type: "boolean", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_request_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_request_messages_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_messages_requests_request_id",
                        column: x => x.request_id,
                        principalSchema: "requests",
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_coordination_entries_organization_id",
                schema: "requests",
                table: "coordination_entries",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_coordination_entries_request_id_occurred_at",
                schema: "requests",
                table: "coordination_entries",
                columns: new[] { "request_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_request_messages_organization_id",
                schema: "requests",
                table: "request_messages",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_messages_request_id_at",
                schema: "requests",
                table: "request_messages",
                columns: new[] { "request_id", "at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "coordination_entries",
                schema: "requests");

            migrationBuilder.DropTable(
                name: "request_messages",
                schema: "requests");

            migrationBuilder.DropColumn(
                name: "assigned_at",
                schema: "requests",
                table: "requests");

            migrationBuilder.DropColumn(
                name: "identity_check_note",
                schema: "requests",
                table: "requests");

            migrationBuilder.DropColumn(
                name: "identity_checked_at",
                schema: "requests",
                table: "requests");

            migrationBuilder.DropColumn(
                name: "identity_checked_by_user_id",
                schema: "requests",
                table: "requests");
        }
    }
}
