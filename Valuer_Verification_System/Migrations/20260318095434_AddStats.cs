using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Valuer_Verification_System.Migrations
{
    /// <inheritdoc />
    public partial class AddStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Section78Stats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FinancialYear = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "2025_26"),
                    Period = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExtractionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SvCapturePeriod = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WeekTableName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TotalSvEntries = table.Column<int>(type: "int", nullable: false),
                    OrdinaryEntries = table.Column<int>(type: "int", nullable: false),
                    SplitValuesEntries = table.Column<int>(type: "int", nullable: false),
                    Leases = table.Column<int>(type: "int", nullable: false),
                    NoticesSuitableForPosting = table.Column<int>(type: "int", nullable: false),
                    RemovedEntries = table.Column<int>(type: "int", nullable: false),
                    EntriesWithZeroExtent = table.Column<int>(type: "int", nullable: false),
                    NoticesNotSuitable = table.Column<int>(type: "int", nullable: false),
                    SuitableForEmailing = table.Column<int>(type: "int", nullable: false),
                    SentByEmailNoPostal = table.Column<int>(type: "int", nullable: false),
                    MunicipalOwnedProperties = table.Column<int>(type: "int", nullable: false),
                    ReviewPeriodOpen = table.Column<DateOnly>(type: "date", nullable: true),
                    ReviewPeriodClosed = table.Column<DateOnly>(type: "date", nullable: true),
                    OpenYN = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false, defaultValue: "Y"),
                    ReviewsReceived = table.Column<int>(type: "int", nullable: true),
                    Closed = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    CompiledBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    BatchId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Section78Stats", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Section78Stats_FY_Period",
                table: "Section78Stats",
                columns: new[] { "FinancialYear", "Period" });

            migrationBuilder.CreateIndex(
                name: "IX_Section78Stats_FY_WeekTable",
                table: "Section78Stats",
                columns: new[] { "FinancialYear", "WeekTableName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Section78Stats");
        }
    }
}
