-- Rights of the application's own login on the Online Egitim database (example).
--
-- Run it as the database owner, connected to the application's database, after the first start with
-- the owner's connection has created or upgraded the schema. Afterwards point the application's
-- connection string at this login. It may read and write the account and course tables, but on the
-- setup key table only read and mark a key used: it cannot create, replace, extend or delete a setup
-- key. Keys come only from docs/issue-setup-key.sql, run by the database owner.
--
-- Create the login and the user first, for example:
--   CREATE LOGIN [DOMAIN\OnlineEgitimApp] FROM WINDOWS;        -- or a SQL login with a password
--   CREATE USER [OnlineEgitimApp] FOR LOGIN [DOMAIN\OnlineEgitimApp];
-- then set @AppUser below to the user's name.

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @AppUser sysname = N'OnlineEgitimApp';
DECLARE @user nvarchar(300) = QUOTENAME(@AppUser);
DECLARE @sql nvarchar(max) = N'';

-- Accounts, courses, notes and enrollments: the application never deletes rows (a deleted course and a
-- left enrollment are marked, not removed). On Egitmenler the trigger TR_Egitmenler_Rol lets this login
-- lower a role but never raise one: only the database owner makes administrators (docs/promote-admin.sql).
SELECT @sql += N'GRANT SELECT, INSERT, UPDATE ON dbo.' + QUOTENAME(t.name) + N' TO ' + @user + N';' + NCHAR(10)
FROM (VALUES (N'Ogrenci'), (N'Egitmenler'), (N'Dersler'), (N'Notlar'), (N'OgrenciKurslar')) AS t(name);

-- Setup keys: read, and mark used. No INSERT, no DELETE, no UPDATE of the hash or the expiry.
SET @sql += N'GRANT SELECT ON dbo.KurulumAnahtarlari TO ' + @user + N';' + NCHAR(10)
    + N'GRANT UPDATE (Tuketildi, TuketimTarihi) ON dbo.KurulumAnahtarlari TO ' + @user + N';' + NCHAR(10)
    + N'DENY INSERT, DELETE ON dbo.KurulumAnahtarlari TO ' + @user + N';' + NCHAR(10)
    + N'DENY UPDATE (AnahtarID, AnahtarOzeti, Olusturma, SonKullanma) ON dbo.KurulumAnahtarlari TO ' + @user + N';' + NCHAR(10)
    -- The start-up check of the migrations only reads their history.
    + N'GRANT SELECT ON dbo.__EFMigrationsHistory TO ' + @user + N';' + NCHAR(10);

EXEC sys.sp_executesql @sql;
