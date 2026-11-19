-- Creates a single use setup key for the Online Egitim database and shows it once.
--
-- Run it as the database owner (db_owner or sysadmin), connected to the application's database,
-- for example: sqlcmd -S <server> -d <database> -E -i docs\issue-setup-key.sql
-- The application's own login cannot run it: it may only read KurulumAnahtarlari and mark a key
-- used (see docs/grant-app-login.sql).
--
-- The key is 16 random bytes (128 bits) from CRYPT_GEN_RANDOM, shown as 32 hexadecimal characters.
-- Only its SHA-256 hash is stored. It works for 24 hours, once, and only while no teacher can sign
-- in with a password: for the first teacher of a database that has students, for the first password
-- of a teacher account without one, or for a student without a password who is enrolled in no course.
-- Give the key to the founder over a safe channel; it is not stored anywhere else.

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- 1 revokes every earlier unused key (marks it used), for example when a key was lost or shown to
-- someone else. 0 keeps them; each one works until it expires or is used.
DECLARE @RevokeUnusedKeys bit = 0;

DECLARE @Key char(32) = CONVERT(char(32), CRYPT_GEN_RANDOM(16), 2);
DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @Expires datetime2 = DATEADD(HOUR, 24, @Now);

BEGIN TRANSACTION;

-- The same lock the application takes first, so a teacher who gets a password at this moment is seen.
IF EXISTS (SELECT 1 FROM dbo.Egitmenler WITH (UPDLOCK, HOLDLOCK) WHERE SifreBicimi <> 2)
BEGIN
    ROLLBACK TRANSACTION;
    THROW 50001, N'A teacher can sign in with a password; setup keys are not accepted, so none was created.', 1;
END;

IF @RevokeUnusedKeys = 1
    UPDATE dbo.KurulumAnahtarlari SET Tuketildi = 1, TuketimTarihi = @Now WHERE Tuketildi = 0;

INSERT dbo.KurulumAnahtarlari (AnahtarOzeti, Olusturma, SonKullanma, Tuketildi)
VALUES (HASHBYTES('SHA2_256', @Key), @Now, @Expires, 0);

COMMIT TRANSACTION;

-- Shown once, in groups of four; dashes, spaces and case do not matter when it is typed.
SELECT
    SUBSTRING(@Key, 1, 4) + '-' + SUBSTRING(@Key, 5, 4) + '-' + SUBSTRING(@Key, 9, 4) + '-' + SUBSTRING(@Key, 13, 4) + '-'
    + SUBSTRING(@Key, 17, 4) + '-' + SUBSTRING(@Key, 21, 4) + '-' + SUBSTRING(@Key, 25, 4) + '-' + SUBSTRING(@Key, 29, 4) AS SetupKey,
    @Expires AS ExpiresUtc;
