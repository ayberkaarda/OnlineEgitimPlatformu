using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineEgitim.Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Egitmenler",
                columns: table => new
                {
                    EgitmenID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UzmanlikAlani = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AdSoyad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Sifre = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SifreBicimi = table.Column<int>(type: "int", nullable: false),
                    BasarisizGiris = table.Column<int>(type: "int", nullable: false),
                    KilitBitis = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Egitmenler", x => x.EgitmenID);
                    table.CheckConstraint("CK_Egitmenler_BasarisizGiris", "[BasarisizGiris] >= 0");
                    table.CheckConstraint("CK_Egitmenler_SifreBicimi", "[SifreBicimi] IN (0, 1, 2)");
                });

            migrationBuilder.CreateTable(
                name: "Ogrenci",
                columns: table => new
                {
                    OgrenciID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Isim = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Sifre = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SifreBicimi = table.Column<int>(type: "int", nullable: false),
                    BasarisizGiris = table.Column<int>(type: "int", nullable: false),
                    KilitBitis = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ogrenci", x => x.OgrenciID);
                    table.CheckConstraint("CK_Ogrenci_BasarisizGiris", "[BasarisizGiris] >= 0");
                    table.CheckConstraint("CK_Ogrenci_SifreBicimi", "[SifreBicimi] IN (0, 1, 2)");
                });

            migrationBuilder.CreateTable(
                name: "Dersler",
                columns: table => new
                {
                    DersID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DersAdi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EgitmenID = table.Column<int>(type: "int", nullable: false),
                    Konu = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dersler", x => x.DersID);
                    table.ForeignKey(
                        name: "FK_Dersler_Egitmenler",
                        column: x => x.EgitmenID,
                        principalTable: "Egitmenler",
                        principalColumn: "EgitmenID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notlar",
                columns: table => new
                {
                    NotID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DersID = table.Column<int>(type: "int", nullable: false),
                    Icerik = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notlar", x => x.NotID);
                    table.ForeignKey(
                        name: "FK_Notlar_Dersler",
                        column: x => x.DersID,
                        principalTable: "Dersler",
                        principalColumn: "DersID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OgrenciKurslar",
                columns: table => new
                {
                    OgrenciID = table.Column<int>(type: "int", nullable: false),
                    DersID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OgrenciKurslar", x => new { x.OgrenciID, x.DersID });
                    table.ForeignKey(
                        name: "FK_OgrenciKurslar_Dersler",
                        column: x => x.DersID,
                        principalTable: "Dersler",
                        principalColumn: "DersID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OgrenciKurslar_Ogrenci",
                        column: x => x.OgrenciID,
                        principalTable: "Ogrenci",
                        principalColumn: "OgrenciID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dersler_EgitmenID",
                table: "Dersler",
                column: "EgitmenID");

            migrationBuilder.CreateIndex(
                name: "IX_Egitmenler_AdSoyad",
                table: "Egitmenler",
                column: "AdSoyad",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notlar_DersID",
                table: "Notlar",
                column: "DersID");

            migrationBuilder.CreateIndex(
                name: "IX_Ogrenci_Email",
                table: "Ogrenci",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ogrenci_Isim",
                table: "Ogrenci",
                column: "Isim",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OgrenciKurslar_DersID",
                table: "OgrenciKurslar",
                column: "DersID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notlar");

            migrationBuilder.DropTable(
                name: "OgrenciKurslar");

            migrationBuilder.DropTable(
                name: "Dersler");

            migrationBuilder.DropTable(
                name: "Ogrenci");

            migrationBuilder.DropTable(
                name: "Egitmenler");
        }
    }
}
