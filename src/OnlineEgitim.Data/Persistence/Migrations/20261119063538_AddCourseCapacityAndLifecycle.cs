using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineEgitim.Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseCapacityAndLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AyrilmaTarihi",
                table: "OgrenciKurslar",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kontenjan",
                table: "Dersler",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SilinmeTarihi",
                table: "Dersler",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Dersler_Kontenjan",
                table: "Dersler",
                sql: "[Kontenjan] IS NULL OR [Kontenjan] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Dersler_Kontenjan",
                table: "Dersler");

            migrationBuilder.DropColumn(
                name: "AyrilmaTarihi",
                table: "OgrenciKurslar");

            migrationBuilder.DropColumn(
                name: "Kontenjan",
                table: "Dersler");

            migrationBuilder.DropColumn(
                name: "SilinmeTarihi",
                table: "Dersler");
        }
    }
}
