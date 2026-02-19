using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Valuer_Verification_System.Migrations
{
    /// <inheritdoc />
    public partial class updateExtractRow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AddColumn<string>(
                name: "CatCode",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CatDesc",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalArea",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PropertyId",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RateableArea",
                table: "ExtractRows",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RightDescription",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RightsPremise",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RightsPremiseSize",
                table: "ExtractRows",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RightsStatus",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SendNotice",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SgId",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StandNo",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusCode",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusSubtype",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TownNameDesc",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitKey",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitNo",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitRightsKey",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitType",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValuationDate",
                table: "ExtractRows",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValuationRollKey",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValuationSplitInd",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValuationSplitNo",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValuationType",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "WefDate",
                table: "ExtractRows",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CatCode",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "CatDesc",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "LegalArea",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "RateableArea",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "RightDescription",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "RightsPremise",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "RightsPremiseSize",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "RightsStatus",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "SendNotice",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "SgId",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "StandNo",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "StatusCode",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "StatusSubtype",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "TownNameDesc",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "UnitKey",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "UnitNo",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "UnitRightsKey",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "UnitType",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "ValuationDate",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "ValuationRollKey",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "ValuationSplitInd",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "ValuationSplitNo",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "ValuationType",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "WefDate",
                table: "ExtractRows");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                table: "ExtractRows",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "ExtractRows",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
