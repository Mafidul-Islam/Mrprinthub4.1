# MR Print Hub — Implementation Plan

Repository is currently empty (just the folder skeleton). We build in
phases; each phase ends with a build, tests passing, a summary of changed
files, and manual test steps — then we stop and wait before continuing.

## Phase 0 — Solution Scaffolding
- Create the `.sln` and all csproj files matching the folder structure.
- Target `net8.0` (or current .NET LTS) for all projects;
  `net8.0-windows` for Desktop/Service where WPF/Windows APIs are needed.
- Wire project references: Desktop/Service → Core; Server → Core, Security,
  Storage, Database, QR; Network is referenced by Service only.
- Add `.editorconfig`, `.gitignore`, nullable reference types + implicit
  usings enabled everywhere.
- Empty "Hello World" smoke test in each test project to confirm the test
  runner works end to end.
- **Exit criteria**: `dotnet build` succeeds, `dotnet test` runs (trivially
  passing) across the whole solution.

## Phase 1 — Core Contracts & Database
- `MRPrintHub.Core`: shared DTOs/enums (`UploadStatus`, `SessionInfo`,
  `AppSettingsDto`, IPC message contracts).
- `MRPrintHub.Database`: EF Core `DbContext`, entities (`Settings`,
  `Sessions`, `Uploads`, `ApplicationLogs`), first migration, a small
  repository layer so callers don't touch `DbContext` directly.
- Unit tests: migrations apply cleanly to a fresh SQLite file; repository
  CRUD round-trips.
- **Exit criteria**: DB creates itself on first run in a temp folder; tests
  green.

## Phase 2 — Security Primitives
- `MRPrintHub.Security`: token generator (CSPRNG), filename sanitizer,
  path-traversal guard, extension allow-list validator, session
  expiry/validation logic (pure functions, no I/O — easy to unit test hard).
- Unit tests specifically for: path traversal payloads (`../..`, absolute
  paths, UNC paths, null bytes, reserved device names), oversized/garbage
  filenames, disallowed extensions, expired/revoked/IP-mismatched sessions.
- **Exit criteria**: this is the most security-sensitive phase — no
  implementation detail skipped, full test list from the master prompt's
  "Security Requirements" section covered.

## Phase 3 — Network Detection
- `MRPrintHub.Network`: active-adapter resolution (prefers adapters with a
  default gateway, excludes virtual/VPN adapters), `NetworkChange`/
  `SystemEvents` watchers, an `INetworkStatusProvider` abstraction the rest
  of the app depends on (so it's mockable in tests).
- Integration-style tests using a fake `INetworkStatusProvider` to simulate
  disconnect/reconnect/IP-change events and assert the expected session
  invalidation callback fires.
- **Exit criteria**: adapter selection logic verified against a matrix of
  mocked adapter lists (Wi-Fi only, Ethernet only, both, VPN present, none
  active).

## Phase 4 — QR Generation
- `MRPrintHub.QR`: wraps QRCoder, builds the upload URL from
  `INetworkStatusProvider` + current session token + configured port,
  renders PNG bytes for the Desktop UI to display/print.
- Unit tests: URL format correctness, regeneration on token change.
- **Exit criteria**: given a fixed IP/port/token, QR payload matches exactly.

## Phase 5 — HTTP Server & Upload Pipeline
- `MRPrintHub.Server`: ASP.NET Core minimal API hosted in-process inside the
  Service (via `IHostedService`/`WebApplication`), bound only to the active
  local IP (from Phase 3).
- Routes: `GET /u/{token}` (serves mobile SPA), `POST /api/upload/{token}`
  (streaming multipart handling per the pipeline in `ARCHITECTURE.md`),
  `GET /api/status/{token}` (lightweight polling for the phone's progress UI
  if needed).
- `MRPrintHub.Storage`: temp→validate→move pipeline, duplicate-filename
  resolution, low-disk-space check.
- Rate limiting + concurrency limiting middleware wired in.
- Integration tests (`WebApplicationFactory` + temp storage dir): successful
  upload, multiple files, oversized file rejection, disallowed extension,
  path-traversal filename attempt, duplicate filename, expired session,
  wrong/garbage token, disk-full simulation (mockable storage check),
  concurrent uploads to the same session.
- **Exit criteria**: every test from the master prompt's upload-related test
  list passes; manual test with a real phone on the same LAN confirmed
  working end-to-end (this is the "no fake upload success" checkpoint).

## Phase 6 — Windows Service Host
- `MRPrintHub.Service`: hosts the Server + Network watcher + Session
  manager as a `BackgroundService`/Generic Host running as a Windows
  Service (`Microsoft.Extensions.Hosting.WindowsServices`), plus the
  named-pipe IPC endpoint for the Desktop app.
- Handles service start/stop/restart cleanly, including graceful shutdown
  of in-flight uploads.
- Tests: service starts/stops without leaking file handles/ports; IPC
  round-trip test (fake client) for status query and upload-event push.
- **Exit criteria**: `sc start/stop MRPrintHubService` works locally in dev
  (via a debug console-mode fallback, since Windows Services aren't
  directly debuggable without attaching).

## Phase 7 — Desktop App (WPF)
- `MRPrintHub.Desktop`: First-run Setup Wizard (6 steps per spec), main
  Dashboard, QR Code screen, Upload History, Storage screen, Settings,
  Logs, About, System Tray with the specified menu.
- All data comes from the Service via IPC — no direct DB/filesystem access
  from Desktop.
- Desktop notifications on upload completion.
- **Exit criteria**: full manual walkthrough of the wizard → dashboard →
  QR → simulated upload → history updates live.

## Phase 8 — Mobile Upload Website
- `MRPrintHub.Web`: static mobile-first HTML/CSS/JS (no framework needed;
  keep it dependency-light so it loads fast on a shop's guest Wi-Fi),
  screens: upload/file-select, selected-file list, progress, success, error,
  session-expired, service-unavailable.
- Served directly by the Server from Phase 5.
- **Exit criteria**: manual test on an actual Android/iPhone browser over
  LAN.

## Phase 9 — Installer
- WiX Toolset project: installs Service + Desktop, creates
  `ProgramData\MRPrintHub` tree, scoped firewall rule, shortcuts, Desktop
  app set to launch at logon, clean uninstall (with the "keep received
  files" choice from `ARCHITECTURE.md`).
- **Exit criteria**: install → first-run wizard → upload → uninstall tested
  on a clean Windows VM.

## Phase 10 — Hardening Pass
- Re-review every item in the master prompt's Security Requirements and
  Testing sections against what's actually implemented; close any gaps.
- Load/soak test: many small concurrent uploads, one large file near the
  size limit, sustained low-disk condition.
- Finalize logging redaction review (no tokens, no file contents, no full
  paths in shipped logs).

---

## Ground rules for how we proceed
- One phase per iteration. After each phase: build, run tests, report
  results, list changed files, give manual test steps, then stop.
- No phase is marked done until its tests are green and its "Exit criteria"
  are met.
- Any requirement that turns out to have multiple valid implementations will
  be flagged with the trade-off and the safer option chosen, before writing
  code — not decided silently mid-implementation.

**Next step**: confirm this plan (or request changes), then we start
Phase 0.
