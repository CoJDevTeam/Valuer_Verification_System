using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Valuer_Verification_System.Migrations
{
    /// <inheritdoc />
    public partial class allowsplitpropertyrows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExtractRows_BatchId_ValuationKey",
                table: "ExtractRows");

            migrationBuilder.AlterColumn<string>(
                name: "ValuationSplitNo",
                table: "ExtractRows",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExtractRows_BatchId_ValuationKey",
                table: "ExtractRows",
                columns: new[] { "BatchId", "ValuationKey" });

            migrationBuilder.CreateIndex(
                name: "IX_ExtractRows_BatchId_ValuationKey_SplitNo",
                table: "ExtractRows",
                columns: new[] { "BatchId", "ValuationKey", "ValuationSplitNo" },
                unique: true,
                filter: "[ValuationSplitNo] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExtractRows_BatchId_ValuationKey",
                table: "ExtractRows");

            migrationBuilder.DropIndex(
                name: "IX_ExtractRows_BatchId_ValuationKey_SplitNo",
                table: "ExtractRows");

            migrationBuilder.AlterColumn<string>(
                name: "ValuationSplitNo",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExtractRows_BatchId_ValuationKey",
                table: "ExtractRows",
                columns: new[] { "BatchId", "ValuationKey" },
                unique: true);
        }
    }
}
