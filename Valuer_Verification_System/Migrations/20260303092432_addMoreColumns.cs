using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Valuer_Verification_System.Migrations
{
    /// <inheritdoc />
    public partial class addMoreColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Valuer",
                table: "ExtractRows",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

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
                name: "Sector",
                table: "ExtractRows",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PropertyType",
                table: "ExtractRows",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PropertyDescription",
                table: "ExtractRows",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "ExtractRows",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "ExtractRows",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Addr1",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Addr2",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Addr3",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Addr4",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Addr5",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CojOwned",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EStatus",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailAddr",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailInd",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Erf",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Extent",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractPeriod",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeasePropertyDesc",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeaseUnitNo",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LetterDate",
                table: "ExtractRows",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LisStreetAddress",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NoticeOpen",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerName",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhysicalAddress",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PremiseAddress",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PremiseId",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PropertyCategory",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PropertyDesc",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PropertyDescription_",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ptn",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Re",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemarkDesc",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReviewCloseDate",
                table: "ExtractRows",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReviewCloseDate2",
                table: "ExtractRows",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewOpen",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReviewOpenDate",
                table: "ExtractRows",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReviewOpenDate2",
                table: "ExtractRows",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RightsUnitType",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SapData",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchemeName",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchemeNameDesc",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchemeNo",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchemeNumber",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchemeYear",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SplitPost",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StAddr2Postal",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StPropertyDescription",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitNoChar",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Usable",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValuationKey2",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Addr1",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "Addr2",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "Addr3",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "Addr4",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "Addr5",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "CojOwned",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "EStatus",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "EmailAddr",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "EmailInd",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "Erf",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "Extent",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "ExtractPeriod",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "LeasePropertyDesc",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "LeaseUnitNo",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "LetterDate",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "LisStreetAddress",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "NoticeOpen",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "OwnerName",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "PhysicalAddress",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "PremiseAddress",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "PremiseId",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "PropertyCategory",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "PropertyDesc",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "PropertyDescription_",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "Ptn",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "Re",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "RemarkDesc",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "ReviewCloseDate",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "ReviewCloseDate2",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "ReviewOpen",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "ReviewOpenDate",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "ReviewOpenDate2",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "RightsUnitType",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "SapData",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "SchemeName",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "SchemeNameDesc",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "SchemeNo",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "SchemeNumber",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "SchemeYear",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "SplitPost",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "StAddr2Postal",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "StPropertyDescription",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "UnitNoChar",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "Usable",
                table: "ExtractRows");

            migrationBuilder.DropColumn(
                name: "ValuationKey2",
                table: "ExtractRows");

            migrationBuilder.AlterColumn<string>(
                name: "Valuer",
                table: "ExtractRows",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

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
                name: "Sector",
                table: "ExtractRows",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PropertyType",
                table: "ExtractRows",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PropertyDescription",
                table: "ExtractRows",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "ExtractRows",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "ExtractRows",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
