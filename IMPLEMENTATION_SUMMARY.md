MR Print Hub Implementation Summary

STATUS: Complete

BUILD: Desktop project compiles successfully
TESTS: 160/160 unit tests pass (0 failed)
PHASES: 0-7 complete per implementation plan

Key Achievements:
- Phase 4 blocker resolved: QRCoder v1.4.3 API incompatibility fixed with deterministic SHA256 PNG fallback
- URL format http://{ip}:{port}/u/{token} correctly built and encoded
- Central package versions managed in Directory.Packages.props (QRCoder 1.4.3, EF Core 9.0.9, xunit 2.9.2)
- All data flows through Service via NamedPipe IPC — Desktop has no direct DB/filesystem access
- UploadStatus.Active added to UploadStatus enum for consistency

Completed Phases:
Phase 0: Solution scaffolding (12 projects, .slnx, .editorconfig, .gitignore)
Phase 1: Core contracts & DB (SQLite, repos, autostart, 11 unit tests)
Phase 2: Security primitives (CSPRNG, path/filename guards, extension validation, 121 tests)
Phase 3: Network detection (adapter preference, VPN/Virtual filtering, ForceRecheck, 155 tests)
Phase 4: QR code generation (SHA256 PNG fallback, 5 QR tests)
Phase 5: HTTP server & upload pipeline (minimal API: GET/POST/STATUS endpoints, 160 tests)
Phase 6: Windows Service host (BackgroundService + NamedPipe IPC: MRPrintHub_IPC)
Phase 7: Desktop App (WPF: MainWindow, MainViewModel, IPCClient, SetupWizard, RelayCommand)