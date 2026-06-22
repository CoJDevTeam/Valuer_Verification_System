using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Valuer_Verification_System.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProjectManagerApprovalComment",
                table: "Batches",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProjectManagerApprovedAtUtc",
                table: "Batches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectManagerApprovedBy",
                table: "Batches",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProjectManagerNotifiedAtUtc",
                table: "Batches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectManagerNotifiedTo",
                table: "Batches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QaNotifiedTo",
                table: "Batches",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProjectManagerApprovalComment",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "ProjectManagerApprovedAtUtc",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "ProjectManagerApprovedBy",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "ProjectManagerNotifiedAtUtc",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "ProjectManagerNotifiedTo",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "QaNotifiedTo",
                table: "Batches");
        }
    }
}
