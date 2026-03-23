using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SOMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgSettingSemesterStart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SemesterStart",
                table: "OrgSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "OrgSettings",
                keyColumn: "SettingId",
                keyValue: 1,
                column: "SemesterStart",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SemesterStart",
                table: "OrgSettings");
        }
    }
}
