using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rahoon.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyMortgageModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Removal of the mortgage-default help model (2026-10-01). Its data is disposable test data and the owner authorised
            // deleting it with its schemas. Shared tables move to «app»; the marketplace, accounts and audit stay. Not reversible.

            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.EnsureSchema(
                name: "files");

            migrationBuilder.EnsureSchema(
                name: "directory");

            migrationBuilder.RenameTable(
                name: "reference_counters",
                schema: "cases",
                newName: "reference_counters",
                newSchema: "app");

            migrationBuilder.RenameTable(
                name: "idempotency_records",
                schema: "admin",
                newName: "idempotency_records",
                newSchema: "app");

            migrationBuilder.Sql("DELETE FROM app.reference_counters WHERE key NOT LIKE 'market:%';");

            migrationBuilder.DropForeignKey(
                name: "fk_audit_events_cases_case_id",
                schema: "audit",
                table: "audit_events");

            migrationBuilder.DropForeignKey(
                name: "fk_memberships_teams_team_id",
                schema: "identity",
                table: "memberships");

            // Every legacy-only schema with its tables, keys, indexes and sequences (the shared tables were moved out above).
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS admin, agreements, analytics, assessment, cases, closure, comms, complaints, documents, ecosystem, providers, referral, requests, sale, solutions CASCADE;");

            // The audit chain cannot lose some events and still verify: the test-data history is cleared and the chain restarts.
            migrationBuilder.Sql("""
                ALTER TABLE audit.audit_events DISABLE TRIGGER trg_audit_events_append_only;
                TRUNCATE audit.audit_events RESTART IDENTITY;
                ALTER TABLE audit.audit_events ENABLE TRIGGER trg_audit_events_append_only;
                ALTER TABLE audit.audit_events ALTER COLUMN data_json TYPE text;
                """);

            migrationBuilder.DropTable(
                name: "invitations",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "role_change_requests",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "teams",
                schema: "identity");

            // Accounts and grants of the withdrawn organizations (lenders, providers, agents, platform), owner accounts, and the
            // sessions and codes of the removed sign-in routes. Individuals (owners/buyers) and the Rahoon team stay.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE legacy_users ON COMMIT DROP AS
                    SELECT u.id FROM identity.users u
                    WHERE u.account_kind = 'Owner'
                       OR (u.account_kind = 'Staff' AND NOT EXISTS (
                            SELECT 1 FROM identity.memberships m JOIN identity.organizations o ON o.id = m.organization_id
                            WHERE m.user_id = u.id AND o.kind = 'Operator'));
                DELETE FROM identity.membership_roles mr USING identity.memberships m, identity.organizations o
                    WHERE mr.membership_id = m.id AND m.organization_id = o.id AND o.kind <> 'Operator';
                DELETE FROM identity.memberships m USING identity.organizations o WHERE m.organization_id = o.id AND o.kind <> 'Operator';
                DELETE FROM identity.role_permissions rp USING identity.roles r, identity.organizations o
                    WHERE rp.role_id = r.id AND r.organization_id = o.id AND o.kind <> 'Operator';
                DELETE FROM identity.roles r USING identity.organizations o WHERE r.organization_id = o.id AND o.kind <> 'Operator';
                DELETE FROM identity.sessions WHERE user_id IN (SELECT id FROM legacy_users) OR scope = 'Owner'
                    OR organization_id IN (SELECT id FROM identity.organizations WHERE kind <> 'Operator');
                DELETE FROM identity.otp_challenges WHERE user_id IN (SELECT id FROM legacy_users) OR purpose NOT IN ('Login', 'IndividualAccess');
                DELETE FROM identity.terms_acceptances WHERE user_id IN (SELECT id FROM legacy_users);
                DELETE FROM identity.individual_profiles WHERE user_id IN (SELECT id FROM legacy_users);
                DELETE FROM app.idempotency_records WHERE user_id IN (SELECT id FROM legacy_users);
                DELETE FROM identity.users WHERE id IN (SELECT id FROM legacy_users);
                DELETE FROM identity.organizations WHERE kind <> 'Operator';
                DELETE FROM identity.role_permissions WHERE permission_key NOT LIKE 'market.%' AND permission_key <> 'directory.manage';
                """);

            migrationBuilder.DropIndex(
                name: "ix_memberships_team_id",
                schema: "identity",
                table: "memberships");

            migrationBuilder.DropIndex(
                name: "ix_individual_profiles_national_id_hash",
                schema: "identity",
                table: "individual_profiles");

            migrationBuilder.DropIndex(
                name: "ix_audit_events_case_id",
                schema: "audit",
                table: "audit_events");

            migrationBuilder.DropIndex(
                name: "ix_audit_events_case_id_seq",
                schema: "audit",
                table: "audit_events");

            migrationBuilder.DropColumn(
                name: "full_name_en",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "mfa_method",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "owner_access_id",
                schema: "identity",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "step_up_until",
                schema: "identity",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "grant",
                schema: "identity",
                table: "role_permissions");

            migrationBuilder.DropColumn(
                name: "allowed_email_domains",
                schema: "identity",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "city",
                schema: "identity",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "default_owner_language",
                schema: "identity",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "license_number",
                schema: "identity",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "mfa_required",
                schema: "identity",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "team_id",
                schema: "identity",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "awareness_opt_in",
                schema: "identity",
                table: "individual_profiles");

            migrationBuilder.DropColumn(
                name: "id_type",
                schema: "identity",
                table: "individual_profiles");

            migrationBuilder.DropColumn(
                name: "identity_assurance",
                schema: "identity",
                table: "individual_profiles");

            migrationBuilder.DropColumn(
                name: "national_id_enc",
                schema: "identity",
                table: "individual_profiles");

            migrationBuilder.DropColumn(
                name: "national_id_hash",
                schema: "identity",
                table: "individual_profiles");

            migrationBuilder.DropColumn(
                name: "national_id_masked",
                schema: "identity",
                table: "individual_profiles");

            migrationBuilder.DropColumn(
                name: "case_id",
                schema: "audit",
                table: "audit_events");

            migrationBuilder.DropColumn(
                name: "case_reference",
                schema: "audit",
                table: "audit_events");

            migrationBuilder.DropForeignKey(
                name: "fk_sale_obligations_obligation_parties_party_id",
                schema: "market",
                table: "sale_obligations");

            // The demo obligation parties were fictional: obligations that named one keep the name as typed text.
            migrationBuilder.Sql("""
                UPDATE market.sale_obligations o SET party_other_name = COALESCE(o.party_other_name, p.name_ar), party_id = NULL
                FROM market.obligation_parties p WHERE o.party_id = p.id;
                """);

            migrationBuilder.DropTable(
                name: "obligation_parties",
                schema: "market");

            migrationBuilder.CreateTable(
                name: "organizations",
                schema: "directory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    normalized_name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    types = table.Column<List<string>>(type: "text[]", nullable: false),
                    website = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    license_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    registration_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    source_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    source_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    verified_on = table.Column<DateOnly>(type: "date", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    origin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    import_keys = table.Column<List<string>>(type: "text[]", nullable: false),
                    last_imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    admin_edited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    admin_edited_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_directory_organizations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_organizations_import_keys",
                schema: "directory",
                table: "organizations",
                column: "import_keys")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_organizations_normalized_name_ar",
                schema: "directory",
                table: "organizations",
                column: "normalized_name_ar",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_organizations_types",
                schema: "directory",
                table: "organizations",
                column: "types")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.AddColumn<string>(
                name: "party_name",
                schema: "market",
                table: "sale_obligations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_sale_obligations_directory_organizations_party_id",
                schema: "market",
                table: "sale_obligations",
                column: "party_id",
                principalSchema: "directory",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddColumn<Guid>(
                name: "preferred_financier_id",
                schema: "market",
                table: "buyer_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preferred_financier_name",
                schema: "market",
                table: "buyer_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_buyer_requests_preferred_financier_id",
                schema: "market",
                table: "buyer_requests",
                column: "preferred_financier_id");

            migrationBuilder.AddForeignKey(
                name: "fk_buyer_requests_directory_organizations_preferred_financier_",
                schema: "market",
                table: "buyer_requests",
                column: "preferred_financier_id",
                principalSchema: "directory",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateTable(
                name: "outbound_sms",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbound_sms", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbound_sms_created_at",
                schema: "app",
                table: "outbound_sms",
                column: "created_at");

            migrationBuilder.CreateTable(
                name: "file_blobs",
                schema: "files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_file_blobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stored_files",
                schema: "files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    storage_ref = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stored_files", x => x.id);
                    table.CheckConstraint("ck_stored_files_size", "size_bytes > 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_stored_files_provider_storage_ref",
                schema: "files",
                table: "stored_files",
                columns: new[] { "provider", "storage_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stored_files_subject_type_subject_id",
                schema: "files",
                table: "stored_files",
                columns: new[] { "subject_type", "subject_id" });

            // Files move into the database: nullable here, filled by LegacyDiskFileMigrator, required by FilesInDatabase.
            migrationBuilder.Sql("ALTER TABLE market.private_documents ADD file_id uuid NULL;");
            migrationBuilder.Sql("ALTER TABLE market.listing_photos ADD file_id uuid NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("The removal of the mortgage-help model is not reversible (rollback: tag legacy-mortgage-final with a database restore).");
        }
    }
}
