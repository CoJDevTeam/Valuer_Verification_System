using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Valuer_Verification_System.Migrations
{
    /// <inheritdoc />
    public partial class AddNoticeQaApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NoticeQaApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    WeekTableName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PrintFolder = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    TotalPrintedNotices = table.Column<int>(type: "int", nullable: false),
                    TotalSampled = table.Column<int>(type: "int", nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ApprovedByName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DataManagerEmailSentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoticeQaApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoticeQaApprovals_Batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "Batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NoticeQaSamples",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NoticeQaApprovalId = table.Column<int>(type: "int", nullable: false),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    ValuationKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PropertyDescription = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    PdfPath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    PdfExists = table.Column<bool>(type: "bit", nullable: false),
                    SampleNo = table.Column<int>(type: "int", nullable: false),
                    CategoryTotal = table.Column<int>(type: "int", nullable: false),
                    CategorySampleSize = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoticeQaSamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoticeQaSamples_NoticeQaApprovals_NoticeQaApprovalId",
                        column: x => x.NoticeQaApprovalId,
                        principalTable: "NoticeQaApprovals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NoticeQaApprovals_BatchId",
                table: "NoticeQaApprovals",
                column: "BatchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NoticeQaSamples_BatchId_Category",
                table: "NoticeQaSamples",
                columns: new[] { "BatchId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_NoticeQaSamples_NoticeQaApprovalId_ValuationKey",
                table: "NoticeQaSamples",
                columns: new[] { "NoticeQaApprovalId", "ValuationKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NoticeQaSamples");

            migrationBuilder.DropTable(
                name: "NoticeQaApprovals");
        }
    }
}
