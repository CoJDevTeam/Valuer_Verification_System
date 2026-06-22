using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Valuer_Verification_System.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBatchTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CleanWeekTableName",
                table: "Batches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExtractCleanedAtUtc",
                table: "Batches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractCleanedBy",
                table: "Batches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractCleaningMessage",
                table: "Batches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractCleaningStatus",
                table: "Batches",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CleanWeekTableName",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "ExtractCleanedAtUtc",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "ExtractCleanedBy",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "ExtractCleaningMessage",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "ExtractCleaningStatus",
                table: "Batches");
        }
    }
}
