using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Valuer_Verification_System.Migrations
{
    /// <inheritdoc />
    public partial class AddExtractFolderImportAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExtractFolderImportAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    ImportedCount = table.Column<int>(type: "int", nullable: false),
                    SkippedCount = table.Column<int>(type: "int", nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FolderPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MachineName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtractFolderImportAudits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExtractFolderImportAudits_RunType",
                table: "ExtractFolderImportAudits",
                column: "RunType");

            migrationBuilder.CreateIndex(
                name: "IX_ExtractFolderImportAudits_StartedAtUtc",
                table: "ExtractFolderImportAudits",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ExtractFolderImportAudits_Success",
                table: "ExtractFolderImportAudits",
                column: "Success");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExtractFolderImportAudits");
        }
    }
}
