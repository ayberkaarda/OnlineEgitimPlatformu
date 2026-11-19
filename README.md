# 🎓 Online Eğitim - Online Learning Platform (C# & SQL Server)

🇬🇧 **English** · 🇹🇷 [Türkçe](README.tr.md)

A Windows Forms application built with .NET 10 and SQL Server with separate panels for students and teachers:
students register, browse the courses, join them and read their notes; teachers create courses, add notes and see
who joined. The code is split into Domain, Business, Data and UI layers; database access uses EF Core, and the
database is created and migrated automatically on startup.

> The application's user interface is in Turkish. Screen and button names below are given with their Turkish
> labels followed by an English translation, e.g. "Ders Seç" (Join Course).

---

## 📦 Features

### Student panel

- ✅ **Registration** ("Öğrenci Kayıt", Student Registration): name, e-mail address and a password of 8-64
  characters. A name or e-mail address that is taken ends like a new registration, so the screen does not tell
  whether someone has an account.
- ✅ **Sign-in** ("Öğrenci Girişi", Student Sign-in) with the name ("İsim Soyisim") and the password.
- ✅ **Course catalogue** ("Öğrenci Paneli", Student Panel): every course with its teacher.
- ✅ **Joining a course** ("Ders Seç", Join Course); a course can be joined once.
- ✅ **Course details** ("Kurs Detayları", Course Details): name, topic, teacher and the number of students; the
  notes only for courses the student joined. Students never see other students' names.
- ✅ **My courses** ("Kurslarım", My Courses) and their notes ("Kurs Notları", Course Notes).

### Teacher panel

- ✅ **Sign-in** ("Öğretmen Giriş", Teacher Sign-in) with the name and the password.
- ✅ **New course** ("Yeni Kurs Ekle", Add Course): name and topic; the course always belongs to the signed-in
  teacher.
- ✅ **Notes** ("Not Ekle", Add Note): multi-line notes, only on the teacher's own courses.
- ✅ **Students of a course** ("Kayıtlı Öğrenciler", Enrolled Students), only for the teacher's own courses.
- ✅ **Recovery code** ("Kurtarma Kodu Üret", Issue Recovery Code) for a passwordless student of the teacher's own
  courses.

### Courses and notes

- ✅ Course names up to 100 characters, topics up to 200 and notes up to 500 (line breaks allowed), without control
  characters; central validators with Turkish messages.
- ✅ A missing and a foreign course get the same answer ("Ders bulunamadı.", Course not found), so ids cannot be
  probed.

### Security

- ✅ Passwords are stored as PBKDF2-HMAC-SHA256 hashes (600,000 iterations, random salt).
- ✅ 5 failed sign-ins lock the account for 15 minutes; unknown, locked, passwordless accounts and wrong passwords
  get the same answer and the same audit code.
- ✅ No default accounts: an empty database opens the first-run setup ("İlk Kurulum", First-run Setup); otherwise
  the founder needs a **setup key** that only the database owner can create (24 hours, single use).
- ✅ **Recovery codes** for accounts the original application left without a password (24 hours, single use,
  stored as a hash).
- ✅ Every service call is checked against the signed-in session's role and ownership.
- ✅ Daily log files without personal data, with anonymous audit codes; write buttons ignore repeated clicks while
  an operation is pending.

---

## 🖼️ Screenshots

The images below show the screens of the original application; the design work will replace them.

| | |
|---|---|
| ![Student sign-in](screenshots/ogrenci_giris.png)<br>**Student sign-in** ("Öğrenci Girişi") | ![Student panel](screenshots/ogrenci_panel.png)<br>**Student panel** ("Öğrenci Paneli") |
| ![Course details](screenshots/kurs_detaylari.png)<br>**Course details** ("Kurs Detayları") | ![My courses](screenshots/kurslarim.png)<br>**My courses** ("Kurslarım") |
| ![Teacher sign-in](screenshots/ogretmen_giris.png)<br>**Teacher sign-in** ("Öğretmen Giriş") | ![Teacher panel](screenshots/ogretmen_panel.png)<br>**Teacher panel** ("Öğretmen Paneli") |

---

## 🔑 Permission Matrix

| Action | Student | Teacher |
|---|---|---|
| Register an account | ✅ student accounts, from the sign-in screen | ❌ (the first teacher comes from the setup) |
| Sign in | ✅ name + password | ✅ name + password |
| List all courses | ✅ | ❌ (sees their own courses) |
| Join a course | ✅ for themselves only | ❌ |
| See course details | ✅ every course; notes only of joined ones | ✅ their own courses only |
| Create a course | ❌ | ✅ owned by themselves |
| Add a note | ❌ | ✅ to their own courses |
| See who joined a course | ❌ (the count only) | ✅ their own courses |
| Issue a recovery code | ❌ | ✅ for passwordless students of their own courses |
| Set a first password with a code | ✅ their own account | ❌ |
| Set a first password with the setup key | ✅ only while enrolled in no course | ✅ while no teacher has a password |
| See or change other accounts' data | ❌ | ❌ |

---

## 🧰 Tech Stack

| Area | Used |
|---|---|
| Platform | .NET 10 (`net10.0-windows` UI, `net10.0` libraries) |
| UI | Windows Forms |
| Data access | Entity Framework Core 10 (SQL Server provider) |
| Database | Microsoft SQL Server (Express is enough) |
| Password hashing | PBKDF2-HMAC-SHA256 (`Rfc2898DeriveBytes.Pbkdf2`) |
| Validation | FluentValidation |
| Configuration / DI | Microsoft.Extensions.Configuration (JSON, user secrets, environment), Microsoft.Extensions.DependencyInjection |
| Logging | Serilog (file sink, daily rolling) |
| Testing | xUnit v3, Moq, EF Core SQLite (in-memory), optional real SQL Server tests |
| CI | GitHub Actions (`.github/workflows/ci.yml`) |

---

## 🗂️ Project Structure

```text
OnlineEgitimPlatformu/
├── src/
│   ├── OnlineEgitim.Domain/     # Entities (Student, Teacher, Course, CourseNote, Enrollment, SetupKey) and repository contracts
│   ├── OnlineEgitim.Business/   # Auth, setup, recovery, course and enrollment services, Security/ (PBKDF2, lockout, setup keys), Validation/ (FluentValidation), Auditing/
│   ├── OnlineEgitim.Data/       # EF Core DbContext, configurations, Migrations/, legacy upgrade, demo data, repositories
│   └── OnlineEgitim.UI/         # WinForms screens, Infrastructure/ (errors, logging, dialogs, write guard), Program.cs (composition root)
├── tests/
│   ├── OnlineEgitim.Business.Tests/  # Validators, hashing, lockout, roles, ownership, setup and recovery (Moq)
│   ├── OnlineEgitim.Data.Tests/      # Services and repositories on SQLite; races, setup keys and legacy upgrade on SQL Server
│   └── OnlineEgitim.UI.Tests/        # Configuration, errors, logs, dialogs, focus and write guards (no windows shown)
├── assets/                      # Original logo, background and icon with their SVG sources
├── docs/
│   ├── THREAT_MODEL.md          # Threat model
│   ├── issue-setup-key.sql      # Setup key, run by the database owner
│   └── grant-app-login.sql      # Rights of the application's own login
├── screenshots/                 # Screens of the original application
├── .github/workflows/ci.yml     # Build, test and release workflow
├── CHANGELOG.md
└── onlineegitimplatformu.sln
```

```mermaid
flowchart TD
    UI["OnlineEgitim.UI<br/>WinForms, DI, error handling, Serilog"] --> BUS["OnlineEgitim.Business<br/>Auth, setup, recovery, course and enrollment services,<br/>FluentValidation, PBKDF2, audit events"]
    UI --> DATA["OnlineEgitim.Data<br/>EF Core, repositories, migrations, legacy upgrade"]
    BUS --> DOM["OnlineEgitim.Domain<br/>Entities, repository contracts"]
    DATA --> DOM
    DATA --> DB[("SQL Server<br/>OnlineEgitim")]
    DBA(["Database owner"]) -.->|"issue-setup-key.sql<br/>grant-app-login.sql"| DB
    UI -.-> LOG[["%LOCALAPPDATA%\OnlineEgitim\logs"]]
```

The UI references Data only to register dependencies at startup; forms work through the Business services, which
take the signed-in session on every call. All database access is asynchronous and parameterised. The provider is
configured through DI (`AddDataAccess`), so tests can use SQLite in-memory.

---

## 🛠️ Setup and Usage

### Requirements

- .NET 10 SDK (Windows)
- SQL Server (for example SQL Server Express)

### 1. Configuration

Provide a connection string named `OnlineEgitim`. Any of the following works (later ones override earlier ones):

1. **`appsettings.json`**: copy `src/OnlineEgitim.UI/appsettings.example.json` to `appsettings.json` in the same
   folder and edit `ConnectionStrings:OnlineEgitim` (the server name for your environment). `appsettings.json` is
   git-ignored.

   ```json
   {
     "ConnectionStrings": {
       "OnlineEgitim": "Server=localhost\\SQLEXPRESS;Database=OnlineEgitim;Trusted_Connection=True;TrustServerCertificate=True;"
     },
     "Seed": {
       "Demo": false
     }
   }
   ```

2. **User secrets**:

   ```powershell
   dotnet user-secrets set "ConnectionStrings:OnlineEgitim" "<connection string>" --project src/OnlineEgitim.UI
   ```

3. **Environment variable** `ConnectionStrings__OnlineEgitim`.
4. **Environment variable** `EGITIM_CONNECTION` (highest priority).

> Inside JSON the `\` in `localhost\SQLEXPRESS` is written as `\\`. `TrustServerCertificate=True` is meant for a
> local development server only.

If no connection string is found, the application shows a Turkish message explaining these options and exits.

### 2. Running and migrations

```powershell
dotnet build -c Release
dotnet run --project src/OnlineEgitim.UI
```

Or open `onlineegitimplatformu.sln` in Visual Studio. The database is created on the first start and pending EF Core
migrations are applied on every start, so no manual `dotnet ef database update` is needed. Run that first start, and
every later start after a schema update, with the database owner's connection (see step 4).

To work with the migrations:

```powershell
dotnet tool restore
dotnet ef migrations list --project src/OnlineEgitim.Data
dotnet ef migrations add <Name> --project src/OnlineEgitim.Data --output-dir Persistence/Migrations
```

The design-time factory reads `EGITIM_CONNECTION` and otherwise uses a local SQL Server Express instance.

### 3. First run, the first teacher and the setup key

- There is no default account. If the database is **completely empty**, the application opens the first-run setup
  ("İlk Kurulum") and the first teacher is created without a key.
- A setup key is accepted only while **no teacher can sign in with a password**: for the first teacher of a database
  that has students but no teacher (the sign-in link "İlk öğretmen kurulumu (kurulum anahtarı)", First Teacher Setup
  (setup key)), for the first password of a teacher without one ("Şifresiz hesabı kurtar" → "Öğretmen (kurulum
  anahtarı)", Recover Account Without Password → Teacher (setup key)), and **only** for a passwordless student who is
  enrolled in no course (the same screen, the key instead of a code). For a student enrolled in a course the key is
  refused and stays unused; that course's teacher issues a recovery code. Once a teacher has a password, no key is
  accepted.
- Only the **database owner** (db_owner or DBA) creates keys:

  ```powershell
  sqlcmd -S <server> -d <database> -E -i docs\issue-setup-key.sql
  ```

  The script shows a 128 bit key from `CRYPT_GEN_RANDOM` once (32 hexadecimal characters) and stores only its
  SHA-256 hash; the key works for **24 hours**, **once**. `@RevokeUnusedKeys = 1` revokes earlier unused keys. The
  application cannot create keys.
- The key is used up in the same database transaction as the account write: a failed write does not spend it, and
  a used key cannot be used again from another Windows profile or computer. The expiry is checked again against the
  database clock when the key is used up, so a key that expires while the request waits for a lock is refused.
- A wrong, expired or used key never locks or revokes the founder's key; such a refusal returns after a short fixed
  pause. Refusals that do not check a key (a teacher with a password exists, a field is missing) return at once. The
  key is never logged or stored in plain text.
- `EGITIM_SETUP_KEY` only fills in the key field; the value is checked against the database like a typed key and
  grants nothing by itself. If it is not a key (32 hexadecimal characters once dashes and spaces are removed), the
  form shows a configuration warning.

### 4. The application's own login

Run the first start (migrations) with the database owner's connection, then switch the application to a separate
login. `docs/grant-app-login.sql` gives that login read and write rights on the account and course tables and, on
`KurulumAnahtarlari`, only `SELECT` and `UPDATE` of `Tuketildi`/`TuketimTarihi`; inserting, deleting or changing the
hash or expiry of a key is denied.

- The script only grants rights; it does **not** remove role memberships. The application's login must not be
  `db_owner` or `sysadmin` and must not own the database: such a login could write its own setup key.
- Pending migrations need more than these rights. Apply every schema update with the owner's connection first, then
  return to the restricted login.

### 5. Recovering accounts without a password

Accounts the original application left without a password cannot sign in with an empty or any other password. Their
first password is set while the account keeps its id and its enrollments:

- **Student:** in the teacher panel, "Kurtarma Kodu Üret" issues a single use code valid for 24 hours (only its hash is
  stored), only for a student enrolled in one of that teacher's own courses; for another teacher's student or a
  student without a course the request is refused as if the student did not exist. The student enters the name, the
  code and the new password under "Şifresiz hesabı kurtar" on the sign-in screen. Wrong codes count towards the
  sign-in lockout.
- **Student enrolled in no course:** while no teacher can sign in with a password, the setup key in the code field.
- **Teacher:** the same screen with "Öğretmen (kurulum anahtarı)" and the setup key, once.

### 6. Upgrading the original database

A database created by the original application is upgraded in place on the first start, keeping its rows and ids.
Records whose identity is unclear (missing, padded or duplicate names or e-mail addresses, an enrollment key other
than `OgrenciID` and `DersID`, duplicate enrollments) stop the upgrade before any change; the error window and the log
show only the problem kind and its count. Fix the records and start the application again. Plain text passwords of
the original application are replaced with a hash on each account's next successful sign-in. Take a backup before the
first start with this version and keep it protected: it contains those plain text passwords.

### 7. Demo mode

Demo mode is off unless it is switched on explicitly, with the environment variable `EGITIM_DEMO=1` or the setting
`"Seed": { "Demo": true }`. Only then, and only when the database has no account or course yet, are fictional
accounts and courses added on start:

| Role | Sign-in name (demo mode only) | Password (demo mode only) |
|---|---|---|
| Teacher | `Deniz Örnek` (Yazılım), `Ece Taslak` (Tasarım) | `Demo.Ogretmen1`, `Demo.Ogretmen2` |
| Student | `Mert Deneme`, `Selin Deneme` | `Demo.Ogrenci1`, `Demo.Ogrenci2` |

Never switch demo mode on for a database with real data.

### 8. Logs

Logs are written to `%LOCALAPPDATA%\OnlineEgitim\logs\onlineegitim-YYYYMMDD.log`, one file per day. Files dated more
than 30 days ago are deleted, and at most 30 files are kept. Error entries contain only exception types, HResult codes
and the method names of the stack trace (no file paths); refused operations log only the exception type. Audit
entries are a fixed code and a role, for example:

```text
2026-11-19 10:42:07.118 +03:00 [INF] Audit auth.sign_in.failed student
2026-11-19 10:42:31.502 +03:00 [INF] Audit auth.sign_in.succeeded student
2026-11-19 10:58:12.034 +03:00 [INF] Audit course.note.added teacher
```

Names, e-mail addresses, passwords, recovery codes, setup keys, note contents and exception messages are never
written to the log.

---

## 🔐 Security

See [docs/THREAT_MODEL.md](docs/THREAT_MODEL.md) for the threat model.

- Passwords are hashed with PBKDF2; the stored format is kept in its own column, so a value is never read in another
  format. Plain text passwords of the original application are upgraded on the next successful sign-in.
- Lockout counters are updated atomically, and a successful sign-in is written only while the account is still
  unlocked, so concurrent attempts cannot clear a fresh lock.
- Roles and ownership are checked in the services and repeated in the database writes; a missing and a foreign
  course get the same answer.
- Setup keys come only from the database owner; recovery codes only from the student's own teacher.
- Connection strings are kept out of the repository (`appsettings.json` is ignored; user secrets and environment
  variables are supported). All queries go through EF Core with parameters.
- Users only see Turkish messages; raw exception text is never shown.

---

## 🧪 Testing

### Manual testing

1. On an empty database, create the first teacher in "İlk Kurulum"; a second setup is refused.
2. Register a student with an invalid e-mail address or a 7 character password; then register correctly and sign in.
3. Enter a wrong password five times: the sixth attempt is refused with the same message even with the right
   password, and works again after 15 minutes.
4. As the teacher, create a course and add a note; as the student, join the course and read the note.
5. Stop SQL Server and check that a friendly Turkish error is shown and the log file contains only types and
   method names.

### Automated tests

```powershell
dotnet test -c Release
```

| Project | Type | Scope | Result (`-c Release`) |
|---|---|---|---|
| `tests/OnlineEgitim.Business.Tests` | Unit | Validator limits and message order, PBKDF2 and format upgrade, lockout, uniform refusals and audit codes, roles and ownership of courses, notes and enrollments, setup keys and recovery with strict Moq mocks | 193 passed |
| `tests/OnlineEgitim.Data.Tests` | Integration | Services and repositories on SQLite in-memory (lockout, hash upgrade, ownership, recovery, refused input); enrollment, sign-in, setup and setup key races, a key expiring while waiting, the application login's rights, the legacy upgrade and its lock on SQL Server | 61 passed with SQL Server and `EGITIM_LEGACY_BACKUP` (60 and 1 skipped without the backup); 42 passed, 19 skipped without SQL Server |
| `tests/OnlineEgitim.UI.Tests` | Unit | Example configuration, error messages, log content, retention and files, dialog helper, focus after unlock, write guards, latest-wins loading, neutral branding | 58 passed |

The SQL Server tests use throw-away databases on the server in `EGITIM_TEST_SQLSERVER` (default: local SQL Express
with Windows authentication) and are skipped when `EGITIM_SKIP_SQL_TESTS=1` is set or no server is reachable. The
temporary databases are dropped afterwards. The test with a restored backup of the original database runs only when
`EGITIM_LEGACY_BACKUP` points to a `.bak` file that the SQL Server service can read.

```powershell
$env:EGITIM_SKIP_SQL_TESTS = "1"; dotnet test -c Release
```

On SQLite the schema is created from the model instead of migrations. The UI tests create forms without showing them,
replace every message box and run serially, so no window opens.

---

## 🚀 CI and Release

`.github/workflows/ci.yml` runs on pushes to `main`, on pull requests and on `v*` tags (Windows, .NET 10):

1. **Build:** `dotnet build -c Release`
2. **Test:** `dotnet test -c Release` (SQL Server tests skipped)
3. **Release (only on `v*` tags):** the tag name is passed through an environment variable and checked against
   `^v[A-Za-z0-9._-]+$`; a framework-dependent win-x64 build is then published and uploaded as the workflow artifact
   `OnlineEgitim-<tag>-win-x64.zip`, with the READMEs, changelogs, the threat model and the two SQL scripts.

To produce the same build locally:

```powershell
dotnet publish src/OnlineEgitim.UI -c Release -r win-x64 --self-contained false -o ./publish
```

The target machine needs the .NET 10 Desktop Runtime. Put `appsettings.json` next to `OnlineEgitim.exe` or set
`EGITIM_CONNECTION`.

---

## 👨‍💻 Developer Notes

- See [CHANGELOG.md](CHANGELOG.md) for the version history.
- `Program.cs` is the composition root: configuration, DI, logging, the audit sink and the global
  `ThreadException` / `UnhandledException` handlers are set up there.
- Business rule violations raise `BusinessRuleException` and permission violations `AccessDeniedException`, both with
  fixed Turkish messages that the UI shows as warnings.
- Validators live in `OnlineEgitim.Business/Validation` and stop at the first failure, so the user sees one message at
  a time.
- New write buttons should go through `ExclusiveOperation` and move the focus with `FocusAfterUnlock`; loads
  triggered by a selection should use `RequestSequence` so only the latest response is shown, and filling a list from
  code must not count as a user selection. Message boxes go through `Dialogs` or `IUserPrompts`, never `MessageBox`
  directly.
- Audit events (`IAuditLog`) carry only an event kind and a role; do not add identifying data to them.

---

**Note:** This is an educational project and is open to further development.
