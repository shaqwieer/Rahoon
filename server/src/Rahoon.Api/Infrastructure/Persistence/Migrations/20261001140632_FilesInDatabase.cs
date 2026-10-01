using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rahoon.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FilesInDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Refuses to run while a marketplace file is still only on disk (run the app's `migrate` command, which copies them).
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM market.private_documents WHERE file_id IS NULL)
                       OR EXISTS (SELECT 1 FROM market.listing_photos WHERE file_id IS NULL) THEN
                        RAISE EXCEPTION 'Marketplace files are not yet copied into the database. Run: dotnet Rahoon.Api.dll migrate';
                    END IF;
                END $$;
                ALTER TABLE market.private_documents ALTER COLUMN file_id SET NOT NULL;
                ALTER TABLE market.listing_photos ALTER COLUMN file_id SET NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "content_type",
                schema: "market",
                table: "private_documents");

            migrationBuilder.DropColumn(
                name: "file_name",
                schema: "market",
                table: "private_documents");

            migrationBuilder.DropColumn(
                name: "sha256",
                schema: "market",
                table: "private_documents");

            migrationBuilder.DropColumn(
                name: "size_bytes",
                schema: "market",
                table: "private_documents");

            migrationBuilder.DropColumn(
                name: "storage_key",
                schema: "market",
                table: "private_documents");

            migrationBuilder.DropColumn(
                name: "content_type",
                schema: "market",
                table: "listing_photos");

            migrationBuilder.DropColumn(
                name: "file_name",
                schema: "market",
                table: "listing_photos");

            migrationBuilder.DropColumn(
                name: "sha256",
                schema: "market",
                table: "listing_photos");

            migrationBuilder.DropColumn(
                name: "size_bytes",
                schema: "market",
                table: "listing_photos");

            migrationBuilder.DropColumn(
                name: "storage_key",
                schema: "market",
                table: "listing_photos");

            migrationBuilder.CreateIndex(
                name: "ix_private_documents_file_id",
                schema: "market",
                table: "private_documents",
                column: "file_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_listing_photos_file_id",
                schema: "market",
                table: "listing_photos",
                column: "file_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_listing_photos_stored_files_file_id",
                schema: "market",
                table: "listing_photos",
                column: "file_id",
                principalSchema: "files",
                principalTable: "stored_files",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_private_documents_stored_files_file_id",
                schema: "market",
                table: "private_documents",
                column: "file_id",
                principalSchema: "files",
                principalTable: "stored_files",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Files are kept in the database since 2026-10-01; moving them back to disk is not supported.");
        }
    }
}
