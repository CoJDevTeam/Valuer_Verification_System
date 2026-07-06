using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Valuer_Verification_System.Migrations
{
    /// <inheritdoc />
    public partial class AddDecisionBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DecisionBy",
                table: "ExtractRows",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

           
            migrationBuilder.CreateIndex(
                name: "IX_ExtractRows_BatchId_Decision_DecisionBy",
                table: "ExtractRows",
                columns: new[] { "BatchId", "Decision", "DecisionBy" });

         
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            

            migrationBuilder.DropIndex(
                name: "IX_ExtractRows_BatchId_Decision_DecisionBy",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "DecisionBy",
                table: "ExtractRows");
        }
    }
}
