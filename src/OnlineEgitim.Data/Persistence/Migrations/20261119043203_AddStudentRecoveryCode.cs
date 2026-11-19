using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineEgitim.Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentRecoveryCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "KurtarmaBitis",
                table: "Ogrenci",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KurtarmaKodu",
                table: "Ogrenci",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KurtarmaBitis",
                table: "Ogrenci");

            migrationBuilder.DropColumn(
                name: "KurtarmaKodu",
                table: "Ogrenci");
        }
    }
}
