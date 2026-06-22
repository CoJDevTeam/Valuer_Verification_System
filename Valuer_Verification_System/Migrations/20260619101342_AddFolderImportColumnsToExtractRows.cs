using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Valuer_Verification_System.Migrations
{
    /// <inheritdoc />
    public partial class AddFolderImportColumnsToExtractRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ResolvedUserId",
                table: "ExtractRows",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolvedValuerEmail",
                table: "ExtractRows",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFileHashSha256",
                table: "ExtractRows",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFileName",
                table: "ExtractRows",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceRowNumber",
                table: "ExtractRows",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "ExtractRows",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValuerMatchComment",
                table: "ExtractRows",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValuerMatchStatus",
                table: "ExtractRows",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExtractRows_BatchId_Username",
                table: "ExtractRows",
                columns: new[] { "BatchId", "Username" });

            migrationBuilder.CreateIndex(
                name: "IX_ExtractRows_SourceFile_RowNumber",
                table: "ExtractRows",
                columns: new[] { "SourceFileHashSha256", "SourceRowNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExtractRows_BatchId_Username",
                table: "ExtractRows");

            migrationBuilder.DropIndex(
                name: "IX_ExtractRows_SourceFile_RowNumber",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "ResolvedUserId",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "ResolvedValuerEmail",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "SourceFileHashSha256",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "SourceFileName",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "SourceRowNumber",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "ValuerMatchComment",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "ValuerMatchStatus",
                table: "ExtractRows");
        }
    }
}
