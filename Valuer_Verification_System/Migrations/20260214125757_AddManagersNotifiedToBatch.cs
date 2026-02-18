using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Valuer_Verification_System.Migrations
{
    /// <inheritdoc />
    public partial class AddManagersNotifiedToBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ManagersNotifiedAtUtc",
                table: "Batches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagersNotifiedBy",
                table: "Batches",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManagersNotifiedAtUtc",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "ManagersNotifiedBy",
                table: "Batches");
        }
    }
}
