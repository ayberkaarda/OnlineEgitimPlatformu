# Changelog

🇬🇧 **English** · 🇹🇷 [Türkçe](CHANGELOG.tr.md)

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- Teachers edit and delete their own courses and give them a capacity (none for no limit, 0 to 1000 seats). A full
  course refuses new students, the capacity cannot go below the students, and a deletion asks for and checks the
  number of enrolled students; deleted courses are kept with their notes and closed enrollments. Seats are decided
  under one `sp_getapplock` lock per course (migration `AddCourseCapacityAndLifecycle`, nullable columns).
- Students leave a course (the seat and the notes are freed at once; joining again from the next day on) and search
  the catalogue by name, topic and teacher with free seat and enrollment filters, literal LIKE matching and stable
  pages; only the answer to the latest search is shown.
- Administrator role: the first-run setup creates the first administrator; `docs/promote-admin.sql` makes a teacher
  an administrator as the database owner, and the trigger `TR_Egitmenler_Rol` refuses promotions by anyone else.
  Administrators search accounts, create teachers and students with a single use code instead of a password, disable
  and enable accounts, make administrators teachers again (never the last active one) and issue reset codes
  (migration `AddAdministration`).
- Password change with the current password and forgotten passwords set with a single use code from an
  administrator or the student's teacher (24 hours, hashed, shown once, never logged, wrong codes lock). Every change
  or reset ends all sessions of the account.
- Every service call checks the session's role, disabled flag and session version against the database, so a
  demoted, disabled or reset account's open session can do nothing more; the panels return to the sign-in.
- Business, SQLite and SQL Server race tests for the new rules, including the last seat, a capacity lowered during
  enrollments, two administrators disabling each other and one code used twice.

- Daily rolling file logs with Serilog under `%LOCALAPPDATA%\OnlineEgitim\logs` (files dated more than 30 days ago
  are deleted and at most 30 files are kept; tests check this with dated files). Names, e-mail addresses,
  passwords, recovery codes, setup keys, note contents and exception messages are never logged.
- Audit entries go to the log file as a fixed code and a role, for example `Audit auth.sign_in.failed student`;
  every failed sign-in writes the same entry.
- Central FluentValidation validators for registration, sign-in, the first-run setup, first passwords, recovery
  codes, courses and notes.
- Validator boundary tests, strict Moq tests for the recovery and setup paths and the service wiring, SQLite tests
  that refused course, note and recovery input writes nothing, and log tests that run a whole session and check the
  file for personal data, keys and codes.
- SQL Server test `SetupKey_ExpiresWhileWaiting_IsRefused` and the policy test
  `SetupKey_EnrolledStudent_IsRefusedAndKeyStaysUnused`.
- Release job in CI: on `v*` tags the tag name is checked, a win-x64 build is published and uploaded as a zip
  artifact.
- English and Turkish README with a permission matrix and an architecture diagram, a threat model
  (`docs/THREAT_MODEL.md`) and this changelog.
- Earlier, in the foundation work: layered `net10.0` projects (Domain, Business, Data, UI) with EF Core migrations
  and DI; PBKDF2 password hashes with lockout; role and ownership checks in every service; the first-run setup with
  the database owner's setup key (`docs/issue-setup-key.sql`, `docs/grant-app-login.sql`); recovery of accounts the
  original application left without a password; an in-place upgrade of the original database; opt-in demo data;
  original artwork; xUnit v3 tests and a CI build.

### Changed

- Redesigned interface with its own identity: a deep indigo violet accent with a warm sun yellow mark on soft
  violet-grey neutrals, a new logo (a screen with rising bars and a spark) rasterized from `assets/logo.svg`, and the
  "Online Eğitim" name. Every screen is rebuilt on one frame (header, inline message strip, cards, footer actions)
  from central theme tokens; sign-in screens have a brand panel. Results, validation and rule refusals appear inline;
  questions use a themed message window. Lists show two lines with a status pill (own enrollment, full, closed,
  free seats, disabled, passwordless) that a long name never hides, and say when they load or are empty; single use
  codes appear once in a large monospaced field. Light, dark (`Appearance:Theme`) and Windows high contrast palettes;
  high contrast changes are followed while the application runs. Per monitor DPI, windows limited to the working
  area with scrolling content, keyboard order and Esc on every screen. Student and teacher panels can sign out, and
  the sign-in screen says why a session ended after a password change.
- `tools/Screenshots` draws the README screenshots offscreen from an invented school and rasterizes the icon.
- The first-run setup and the setup key create the first administrator instead of the first teacher; a setup key is
  refused while any teacher or administrator signs in with a password.
- A teacher's recovery code also works for a student who has a password (forgotten password), and staff accounts set
  a password with a code in "Kodla şifre belirle".
- The test of a setup key expiring while it waits for the teacher lock is deterministic: the attempt's time is fixed
  and the key is expired in the database while the lock is held.

- The input checks written by hand in the services moved to the validators; messages, trimming and the order of the
  checks are unchanged.
- Audit events and errors that were written to the trace output are now written to the log file; refused
  operations log their exception type only.
- The setup key recovers a student only while that student is enrolled in no course; an enrolled student gets a
  recovery code from the course's teacher. The recovery screen, the service documentation and the READMEs say so.
- The README is split into an English `README.md` and a Turkish `README.tr.md`; the folded English section was
  removed.

### Fixed

- Closing the administrator panel while an account or a reset code was being made lost the single use code; the
  close now waits and the window stays open until the code has been read.
- Application lock waits (course, administrators, upgrade) could end as a 30 second command timeout instead of the
  "busy, try again" path; the lock command now outlasts its wait and a refused lock reads "İşlem meşgul".
- Editing a course whose stored capacity is above 1000 (set outside the application) threw; it opens at the limit
  with a warning.
- The application's login could insert an administrator with direct SQL while none existed even though teachers sign
  in with passwords; `TR_Egitmenler_Rol` now refuses that too (migration `RefuseAdministratorBesideStaffPasswords`).
  The README, the threat model and the demo mode section describe this, how long a code stays on screen and that a
  demo database needs `docs/promote-admin.sql` for its administrator.
- A new registration and one with a taken name or e-mail address wrote different audit codes to the user readable
  log file; both now write `auth.student.registration_submitted`.

- A setup key that expired while its transaction waited for the teacher lock was still accepted. The final `UPDATE`
  that uses the key up now checks the expiry against the database clock (`SYSUTCDATETIME()`): such a key is refused,
  no password is set and the key stays unused.
- A setup key set the first password of a passwordless student who was enrolled in a course. It is now refused like a
  wrong key and stays unused.
