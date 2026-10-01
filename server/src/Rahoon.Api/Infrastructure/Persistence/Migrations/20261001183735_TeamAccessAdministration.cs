using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rahoon.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TeamAccessAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "name_en",
                schema: "identity",
                table: "roles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "name_ar",
                schema: "identity",
                table: "roles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "archived_at",
                schema: "identity",
                table: "roles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description_ar",
                schema: "identity",
                table: "roles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_system",
                schema: "identity",
                table: "roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);


            migrationBuilder.AddColumn<string>(
                name: "scope",
                schema: "identity",
                table: "role_permissions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "All");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "status_changed_at",
                schema: "identity",
                table: "memberships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "status_changed_by_user_id",
                schema: "identity",
                table: "memberships",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status_reason",
                schema: "identity",
                table: "memberships",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);


            migrationBuilder.CreateTable(
                name: "staff_invitations",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    role_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    invited_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invited_by_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    accepted_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_invitations", x => x.id);
                    table.ForeignKey(
                        name: "fk_staff_invitations_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "identity",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_staff_invitations_organization_id_email",
                schema: "identity",
                table: "staff_invitations",
                columns: new[] { "organization_id", "email" },
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_staff_invitations_token_hash",
                schema: "identity",
                table: "staff_invitations",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_invitations",
                schema: "identity");

            migrationBuilder.DropColumn(
                name: "archived_at",
                schema: "identity",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "description_ar",
                schema: "identity",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "is_system",
                schema: "identity",
                table: "roles");


            migrationBuilder.DropColumn(
                name: "scope",
                schema: "identity",
                table: "role_permissions");

            migrationBuilder.DropColumn(
                name: "status_changed_at",
                schema: "identity",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "status_changed_by_user_id",
                schema: "identity",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "status_reason",
                schema: "identity",
                table: "memberships");


            migrationBuilder.AlterColumn<string>(
                name: "name_en",
                schema: "identity",
                table: "roles",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "name_ar",
                schema: "identity",
                table: "roles",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);
        }
    }
}
