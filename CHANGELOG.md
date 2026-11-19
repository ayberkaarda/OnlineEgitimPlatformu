# Changelog

🇬🇧 **English** · 🇹🇷 [Türkçe](CHANGELOG.tr.md)

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

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

- The input checks written by hand in the services moved to the validators; messages, trimming and the order of the
  checks are unchanged.
- Audit events and errors that were written to the trace output are now written to the log file; refused
  operations log their exception type only.
- The setup key recovers a student only while that student is enrolled in no course; an enrolled student gets a
  recovery code from the course's teacher. The recovery screen, the service documentation and the READMEs say so.
- The README is split into an English `README.md` and a Turkish `README.tr.md`; the folded English section was
  removed.

### Fixed

- A setup key that expired while its transaction waited for the teacher lock was still accepted. The final `UPDATE`
  that uses the key up now checks the expiry against the database clock (`SYSUTCDATETIME()`): such a key is refused,
  no password is set and the key stays unused.
- A setup key set the first password of a passwordless student who was enrolled in a course. It is now refused like a
  wrong key and stays unused.
