using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineEgitim.Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OturumSurumu",
                table: "Ogrenci",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Pasif",
                table: "Ogrenci",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "KurtarmaBitis",
                table: "Egitmenler",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KurtarmaKodu",
                table: "Egitmenler",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OturumSurumu",
                table: "Egitmenler",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Pasif",
                table: "Egitmenler",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Every existing staff account is a teacher; administrators come from the setup or the owner.
            migrationBuilder.AddColumn<int>(
                name: "Rol",
                table: "Egitmenler",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Egitmenler_Rol",
                table: "Egitmenler",
                sql: "[Rol] IN (1, 2)");

            // Only the database owner makes an account an administrator (docs/promote-admin.sql). Anyone
            // else, such as the application's login, may lower a role and may insert an administrator
            // only while there is none: the first-run setup, which itself needs an empty database or the
            // owner's setup key.
            migrationBuilder.Sql(
                """
                CREATE TRIGGER dbo.TR_Egitmenler_Rol ON dbo.Egitmenler AFTER INSERT, UPDATE AS
                BEGIN
                    SET NOCOUNT ON;
                    IF IS_MEMBER(N'db_owner') = 1 RETURN;
                    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.EgitmenID = i.EgitmenID WHERE i.Rol = 2 AND d.Rol <> 2)
                        OR EXISTS (
                            SELECT 1 FROM inserted i
                            WHERE i.Rol = 2
                              AND NOT EXISTS (SELECT 1 FROM deleted d WHERE d.EgitmenID = i.EgitmenID)
                              AND EXISTS (SELECT 1 FROM dbo.Egitmenler e WHERE e.Rol = 2 AND e.EgitmenID <> i.EgitmenID))
                    BEGIN;
                        THROW 51010, N'Only the database owner can make an account an administrator.', 1;
                    END;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER dbo.TR_Egitmenler_Rol;");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Egitmenler_Rol",
                table: "Egitmenler");

            migrationBuilder.DropColumn(
                name: "OturumSurumu",
                table: "Ogrenci");

            migrationBuilder.DropColumn(
                name: "Pasif",
                table: "Ogrenci");

            migrationBuilder.DropColumn(
                name: "KurtarmaBitis",
                table: "Egitmenler");

            migrationBuilder.DropColumn(
                name: "KurtarmaKodu",
                table: "Egitmenler");

            migrationBuilder.DropColumn(
                name: "OturumSurumu",
                table: "Egitmenler");

            migrationBuilder.DropColumn(
                name: "Pasif",
                table: "Egitmenler");

            migrationBuilder.DropColumn(
                name: "Rol",
                table: "Egitmenler");
        }
    }
}
