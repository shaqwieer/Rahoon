using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rahoon.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IndividualAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "account_kind",
                schema: "identity",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Staff");

            // Existing invited-owner accounts (synthetic owner e-mail) are owners, not staff.
            migrationBuilder.Sql("UPDATE identity.users SET account_kind = 'Owner' WHERE email LIKE 'owner+%@owners.rahoon.local';");

            migrationBuilder.CreateTable(
                name: "individual_profiles",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    national_id_enc = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    national_id_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    national_id_masked = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    id_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    phone_enc = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    phone_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    phone_masked = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    identity_assurance = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    phone_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    terms_version = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    terms_accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    awareness_opt_in = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_individual_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_individual_profiles_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "terms_acceptances",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ip_masked = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_terms_acceptances", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_individual_profiles_national_id_hash",
                schema: "identity",
                table: "individual_profiles",
                column: "national_id_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_individual_profiles_phone_hash",
                schema: "identity",
                table: "individual_profiles",
                column: "phone_hash");

            migrationBuilder.CreateIndex(
                name: "ix_individual_profiles_user_id",
                schema: "identity",
                table: "individual_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_terms_acceptances_user_id_accepted_at",
                schema: "identity",
                table: "terms_acceptances",
                columns: new[] { "user_id", "accepted_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "individual_profiles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "terms_acceptances",
                schema: "identity");

            migrationBuilder.DropColumn(
                name: "account_kind",
                schema: "identity",
                table: "users");
        }
    }
}
