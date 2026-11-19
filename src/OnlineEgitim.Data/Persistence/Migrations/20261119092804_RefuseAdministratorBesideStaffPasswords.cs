using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineEgitim.Data.Persistence.Migrations
{
    /// <summary>
    /// Narrows the INSERT branch of TR_Egitmenler_Rol to the application's own rule: for anyone but the
    /// database owner, a new administrator is refused while another administrator exists or while another
    /// teacher or administrator has a password (SifreBicimi other than 2). Before, the trigger let anyone
    /// insert an administrator while none existed, also in an upgraded database whose teachers sign in
    /// with passwords; the application itself refused that case already.
    /// </summary>
    public partial class RefuseAdministratorBesideStaffPasswords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER dbo.TR_Egitmenler_Rol ON dbo.Egitmenler AFTER INSERT, UPDATE AS
                BEGIN
                    SET NOCOUNT ON;
                    IF IS_MEMBER(N'db_owner') = 1 RETURN;
                    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.EgitmenID = i.EgitmenID WHERE i.Rol = 2 AND d.Rol <> 2)
                        OR EXISTS (
                            SELECT 1 FROM inserted i
                            WHERE i.Rol = 2
                              AND NOT EXISTS (SELECT 1 FROM deleted d WHERE d.EgitmenID = i.EgitmenID)
                              AND EXISTS (
                                  SELECT 1 FROM dbo.Egitmenler e
                                  WHERE e.EgitmenID <> i.EgitmenID AND (e.Rol = 2 OR e.SifreBicimi <> 2)))
                    BEGIN;
                        THROW 51010, N'Only the database owner can make an account an administrator.', 1;
                    END;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER dbo.TR_Egitmenler_Rol ON dbo.Egitmenler AFTER INSERT, UPDATE AS
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
    }
}
