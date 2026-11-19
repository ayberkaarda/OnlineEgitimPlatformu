# Threat Model

A short threat model for the Online Eğitim desktop application. The application stores the names, e-mail
addresses and passwords of students and teachers, the courses teachers create, the notes they add to them and
which student joined which course. Names and e-mail addresses are personal data under the Turkish Personal Data
Protection Law (KVKK, Law No. 6698). This document lists technical measures only; it is not a legal assessment.

## Scope and assets

- **Application:** a Windows Forms client with student and teacher sign-in that talks directly to SQL Server.
- **Assets:**
  - Names and e-mail addresses of students, names and specialties of teachers.
  - Passwords and their stored hashes; recovery codes and setup keys and their hashes.
  - Courses, course notes and enrollments (who joined which course).
  - Sign-in counters and lockout times.
  - The connection string (server name, database, and possibly SQL credentials).
  - Log files under `%LOCALAPPDATA%\OnlineEgitim\logs`.
  - Database backups made by the operator (`.bak` files), including backups of the original application's
    database with plain text passwords.
- **Trust boundaries:** the Windows user session that runs the application; the network link to SQL Server; the
  local file system (settings, logs, backups); the boundary between a signed-in account and the data of other
  accounts; the boundary between the application's SQL login and the database owner.

## Assumptions

- The application runs on trusted workstations; students and teachers sign in on them with their own accounts.
- Every instance connects with the same SQL Server login, so the database itself does not tell accounts apart:
  roles and ownership are enforced by the application.
- The database owner (db_owner or sysadmin) is trusted: they run the migrations, issue setup keys and grant the
  application's login its rights. The operator is the data controller and is responsible for consent, retention,
  deletion requests and the security of the server, its logins and its backups.
- A new database contains no account. Demo accounts (fictional names, passwords published in the README) are
  created only when demo mode is switched on explicitly with `EGITIM_DEMO=1` or `Seed:Demo=true`, and only in a
  database without any account or course.

## Identity

| Threat | Mitigation | Status |
|---|---|---|
| Passwords read from the database or a backup | PBKDF2-HMAC-SHA256 with 600,000 iterations and a 16 byte random salt per password, compared in constant time; the stored format has its own column (`SifreBicimi`), so a value is never read in another format | Mitigated |
| Plain text passwords of the original application | The legacy upgrade marks them `LegacyPlainText`; the first successful sign-in replaces each with a PBKDF2 hash in the same conditional update | Partly mitigated (see residual risks) |
| Accounts the original application left without a password | They are marked `None` and never sign in, whatever is typed (also an empty password); they get a first password only through a verified recovery | Mitigated |
| Online password guessing | 5 failed attempts lock the account for 15 minutes; the counter is increased in one atomic `UPDATE`, attempts during a lock neither count nor extend it, and a success is written only while the account is still unlocked and unchanged, so a concurrent failure cannot be cleared | Mitigated |
| Account enumeration through sign-in answers, timing or the log | Unknown accounts, wrong passwords, locked accounts and accounts without a password get the same message and the same audit code, and each costs one key derivation | Mitigated |
| Account enumeration through registration | A name or e-mail address that is taken ends like a new registration (a hash is derived on both paths, no account is created or changed) | Mitigated (a later sign-in with the newcomer's own password still fails) |
| Lockout used to block a known account | The lock ends by itself after 15 minutes; no permanent lock exists | Accepted |

## Roles and ownership

| Threat | Mitigation | Status |
|---|---|---|
| A student calling teacher operations or the reverse | Every service call takes the session issued by `AuthService` (its constructor is internal) and checks the role first; a wrong role is refused and audited as `access.denied` | Mitigated |
| IDOR: a teacher reading or changing another teacher's course, notes or student list | Lists are queried by the session's own id; details, notes and student lists check the owner, and adding a note repeats the ownership check in the repository's write; a missing and a foreign course get the same answer ("Ders bulunamadı.") | Mitigated |
| A student reading notes of courses they did not join, or other students' names | Notes are returned only to the course's teacher and its students; student names only to the course's teacher, other viewers get the count | Mitigated |
| Joining a course twice | Primary key on student and course; a lost race is reported as "already joined" | Mitigated |
| Invalid or oversized input | Central FluentValidation validators for registration, sign-in, setup, recovery, courses and notes; values are bounded by their columns and control characters are refused | Mitigated |

## Setup key and its accepted limit

| Threat | Mitigation | Status |
|---|---|---|
| Anyone becoming the first teacher of a database that already has students | Only an empty database creates the first teacher without a key. Otherwise a setup key is needed, and keys are accepted only while no teacher can sign in with a password | Mitigated |
| A forged or copied key | Keys are created only by the database owner with `docs/issue-setup-key.sql`: 128 random bits from `CRYPT_GEN_RANDOM`, shown once, stored as a SHA-256 hash, valid for 24 hours and once. The application cannot create keys; `EGITIM_SETUP_KEY` only fills in the form | Mitigated |
| A key used twice, or spent by a failed write | The key is used up by a conditional `UPDATE` in the same serializable transaction as the account write, after the teacher lock; a failed write or commit rolls the use back; two profiles racing with one key write once (SQL Server race test) | Mitigated |
| A key that expires while its transaction waits for the teacher lock | The final `UPDATE` checks the expiry again against the database clock (`SYSUTCDATETIME()`), so a key that expired while waiting is refused, the password stays unset and the key unused (SQL Server test `SetupKey_ExpiresWhileWaiting_IsRefused`) | Mitigated |
| The key used for a student who has a teacher | The key sets the first password only of a student who is enrolled in no course; for an enrolled student it is refused like a wrong key and stays unused, and the course's teacher issues a recovery code instead | Mitigated |
| Guessing keys | 128 bit keys, compared in constant time; each refused key check returns after a fixed one second pause. Wrong keys neither lock nor revoke the founder's key | Mitigated |
| **Accepted limit:** the application's login changing keys itself | The application's SQL login needs `SELECT` and `UPDATE (Tuketildi, TuketimTarihi)` on `KurulumAnahtarlari` and nothing more (`docs/grant-app-login.sql`, tested with a separate database user). If that login is `db_owner`, `sysadmin` or owns the database, anyone with its connection string can insert their own key hash and bypass the setup key. The grant script does not remove such memberships; the operator must run the application with a separate, restricted login | Accepted, documented |
| Schema changes with the restricted login | Pending migrations run on start and need more than the restricted rights: every schema update is applied with the owner's connection first, then the application returns to the restricted login | Documented |

## Recovery codes

| Threat | Mitigation | Status |
|---|---|---|
| A teacher recovering another teacher's student | A code is issued only for a passwordless student enrolled in one of the teacher's own courses; the condition is part of the single `UPDATE` that stores the code, so any other student gets the answer of a missing student and keeps an earlier code | Mitigated |
| Guessing a code | 10 characters (50 bits), stored as a salted PBKDF2 hash, valid for 24 hours and once; wrong codes count towards the sign-in lockout and every refusal costs one verification | Mitigated |
| A code used after a password was set, or twice | Completing a recovery is one conditional `UPDATE` that requires the verified code, no password, an unexpired code and no lock, and removes the code | Mitigated |

## Logging

| Threat | Mitigation | Status |
|---|---|---|
| Personal data or secrets in log files | Error entries hold exception types, HResult codes and method names only, without messages or file paths; refused operations log only the exception type; audit entries are a fixed code and a role (`Audit auth.sign_in.failed student`). Names, e-mail addresses, passwords, recovery codes, setup keys and note contents are never written. A test runs a whole session and checks the file | Mitigated |
| Lock state or account existence disclosed through the log | Every failed sign-in writes the same `auth.sign_in.failed` entry; there is no lock code. A test compares unknown, existing, locked and passwordless accounts | Mitigated |
| Log files read by another application account on the same Windows account | The files hold only anonymous codes, roles and exception types, so they reveal neither who signed in nor which account exists | Mitigated (counts and times of events remain visible) |
| Log files growing without bound | Daily files; files dated more than 30 days ago and any beyond the newest 30 are deleted. Tests create dated files and check which remain | Mitigated |
| Personal data in error dialogs | Users see fixed Turkish messages; raw exception text is never shown | Mitigated |

## Backups

| Threat | Mitigation | Status |
|---|---|---|
| Backups exposing personal data and legacy plain text passwords | `*.bak`, `*.mdf` and `*.ldf` are git-ignored and the backup that the original repository tracked was removed. Backups taken before the upgrade, or while legacy accounts remain, contain plain text passwords: encrypt them, restrict access and delete old ones | Documented |
| Legacy upgrade altering or exposing existing rows | The upgrade runs in one transaction under a checked `sp_getapplock` lock and refuses before any change when rows are ambiguous (missing, padded or duplicate names or e-mail addresses, an unexpected enrollment key), reporting problem codes and counts only | Mitigated |

## Connection security

| Threat | Mitigation | Status |
|---|---|---|
| Connection string leaked through the repository | `appsettings.json` is git-ignored; user secrets and the `EGITIM_CONNECTION` environment variable are supported; only `appsettings.example.json` is committed | Mitigated |
| Interception of the database connection | `TrustServerCertificate=True` in the example is for a local server only; for a remote server use `Encrypt=True` with a trusted certificate | Documented |
| SQL injection | All access goes through EF Core with parameters; the raw statements are constant lock and schema statements, and the key use passes its id as a parameter | Mitigated |
| Direct table access with the application's login | Grant that login only what `docs/grant-app-login.sql` lists, prefer Windows authentication and do not hand the connection string to students | Documented |

## Residual risks

- Accounts migrated from the original application keep their plain text password until their next successful
  sign-in; `SifreBicimi = 1` shows which accounts are affected.
- A passwordless student enrolled in no course cannot be recovered once a teacher can sign in with a password;
  the operator sets such a password in the database or enrolls the student so that a teacher can issue a code.
- Names are the sign-in identifiers and are easy to guess; the lockout and the password are the only protection.
- There is no password change or reset screen for accounts that have a password.
- The setup key is checked with SHA-256, not a slow hash; its 128 random bits make that sufficient.

## Out of scope

- Network services, web access, multi-factor authentication, encryption of data at rest inside the application,
  automated backups and retention policies, and a per-user audit trail of who read which record.
