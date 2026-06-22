using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Valuer_Verification_System.Migrations
{
    /// <inheritdoc />
    public partial class addFolderTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CleanWeekTableName",
                table: "WeekWindows",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExtractCleanedAtUtc",
                table: "WeekWindows",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractCleanedBy",
                table: "WeekWindows",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractCleaningMessage",
                table: "WeekWindows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractCleaningStatus",
                table: "WeekWindows",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectManagerApprovalComment",
                table: "WeekWindows",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProjectManagerApprovedAtUtc",
                table: "WeekWindows",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectManagerApprovedBy",
                table: "WeekWindows",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProjectManagerNotifiedAtUtc",
                table: "WeekWindows",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectManagerNotifiedTo",
                table: "WeekWindows",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QaNotifiedTo",
                table: "WeekWindows",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CleanWeekTableName",
                table: "WeekWindows");

            migrationBuilder.DropColumn(
                name: "ExtractCleanedAtUtc",
                table: "WeekWindows");

            migrationBuilder.DropColumn(
                name: "ExtractCleanedBy",
                table: "WeekWindows");

            migrationBuilder.DropColumn(
                name: "ExtractCleaningMessage",
                table: "WeekWindows");

            migrationBuilder.DropColumn(
                name: "ExtractCleaningStatus",
                table: "WeekWindows");

            migrationBuilder.DropColumn(
                name: "ProjectManagerApprovalComment",
                table: "WeekWindows");

            migrationBuilder.DropColumn(
                name: "ProjectManagerApprovedAtUtc",
                table: "WeekWindows");

            migrationBuilder.DropColumn(
                name: "ProjectManagerApprovedBy",
                table: "WeekWindows");

            migrationBuilder.DropColumn(
                name: "ProjectManagerNotifiedAtUtc",
                table: "WeekWindows");

            migrationBuilder.DropColumn(
                name: "ProjectManagerNotifiedTo",
                table: "WeekWindows");

            migrationBuilder.DropColumn(
                name: "QaNotifiedTo",
                table: "WeekWindows");
        }
    }
}
