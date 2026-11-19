-- Makes an existing teacher an administrator of the Online Egitim database.
--
-- Run it as the database owner (db_owner or sysadmin), connected to the application's database,
-- after setting @TeacherName below, for example: sqlcmd -S <server> -d <database> -E -i docs\promote-admin.sql
-- A setup key is not accepted once a teacher signs in with a password, so a
-- database that already has teachers gets its first administrator this way. The application's own
-- login cannot do this: the script refuses to run for anyone but the database owner, and the trigger
-- TR_Egitmenler_Rol refuses any change that raises an account to administrator unless the database
-- owner makes it.
--
-- An administrator manages accounts and does not teach: the teacher's courses stay in the catalogue
-- with their students and notes, but can be edited again only after the account is a teacher again
-- (an administrator makes another administrator a teacher in the administrator panel). The teacher's
-- open sessions end; they sign in again and get the administrator panel.

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- The sign-in name of the teacher, exactly as the teacher signs in.
DECLARE @TeacherName nvarchar(100) = N'';

IF IS_MEMBER(N'db_owner') IS NULL OR IS_MEMBER(N'db_owner') <> 1
    THROW 50010, N'Run this script as the database owner (db_owner or sysadmin); the application''s login cannot make administrators.', 1;

IF @TeacherName IS NULL OR LTRIM(RTRIM(@TeacherName)) = N''
    THROW 50011, N'Set @TeacherName to the sign-in name of the teacher.', 1;

BEGIN TRANSACTION;

-- The lock the application's setup and recovery take first.
DECLARE @Id int, @Role int;
SELECT @Id = EgitmenID, @Role = Rol FROM dbo.Egitmenler WITH (UPDLOCK, HOLDLOCK) WHERE AdSoyad = @TeacherName;

IF @Id IS NULL
BEGIN
    ROLLBACK TRANSACTION;
    THROW 50012, N'No teacher signs in with that name.', 1;
END;

IF @Role = 2
BEGIN
    ROLLBACK TRANSACTION;
    THROW 50013, N'The account is an administrator already.', 1;
END;

UPDATE dbo.Egitmenler SET Rol = 2, OturumSurumu = OturumSurumu + 1 WHERE EgitmenID = @Id;

COMMIT TRANSACTION;

SELECT
    e.AdSoyad AS Administrator,
    CASE WHEN e.SifreBicimi = 2 THEN N'no password yet: issue a code or use the setup key' ELSE N'signs in with the existing password' END AS SignIn,
    CASE WHEN e.Pasif = 1 THEN N'disabled: enable it in the administrator panel of another administrator' ELSE N'enabled' END AS State,
    (SELECT COUNT(*) FROM dbo.Dersler d WHERE d.EgitmenID = e.EgitmenID AND d.SilinmeTarihi IS NULL) AS CoursesKept
FROM dbo.Egitmenler e
WHERE e.EgitmenID = @Id;
