# MR Print Hub

A Windows desktop application that lets customers at a cyber cafe or print
shop send files from their phone straight to the shop's PC — no app,
account, USB, Bluetooth, or internet connection required on the phone.
The customer just joins the shop's Wi-Fi, scans a QR code, and uploads
files from their mobile browser.

## Status

🚧 Early architecture/planning stage. No application code yet — see
[`ARCHITECTURE.md`](./ARCHITECTURE.md) and
[`IMPLEMENTATION_PLAN.md`](./IMPLEMENTATION_PLAN.md).

## How it works (customer)

1. Connect phone to the shop's Wi-Fi.
2. Scan the QR code shown on the shop PC.
3. A mobile-friendly upload page opens in the browser.
4. Select one or more files and upload.
5. Files appear in the shop's configured folder.

## How it works (shop owner)

1. Install MR Print Hub.
2. First-run wizard: choose a storage folder, the app detects your local
   network and generates a secure QR code.
3. The app runs quietly in the system tray / as a Windows Service and
   starts automatically with Windows.
4. Dashboard shows live status, storage usage, and upload history.

## Project layout

```
MRPrintHub/
  src/
    MRPrintHub.Desktop/     WPF UI (wizard, dashboard, tray)
    MRPrintHub.Service/     Windows Service host (server, network, IPC)
    MRPrintHub.Core/        Shared contracts/DTOs
    MRPrintHub.Server/      ASP.NET Core HTTP API + mobile page hosting
    MRPrintHub.Network/     Local network/adapter detection & monitoring
    MRPrintHub.Storage/     Upload pipeline, temp handling, disk checks
    MRPrintHub.Security/    Tokens, sanitization, validation
    MRPrintHub.Database/    SQLite/EF Core data layer
    MRPrintHub.QR/          QR code generation
    MRPrintHub.Web/         Static mobile upload website
  tests/
    MRPrintHub.UnitTests/
    MRPrintHub.IntegrationTests/
  installer/                WiX Toolset installer project
  docs/
```

## Requirements (planned)

- Windows 10/11
- .NET 8 (LTS)
- Same local Wi-Fi/LAN for shop PC and customer phone (no internet needed)

## Security

Uploads are restricted to a configured folder, filenames are sanitized,
extensions are validated, sessions expire and are tied to the current
network session, and the HTTP server binds only to the local network
adapter — it is never reachable from outside the shop's LAN. See
`ARCHITECTURE.md` §5 for full details.

## Development

This project is being built phase-by-phase per `IMPLEMENTATION_PLAN.md`.
Each phase is built, tested, and reviewed before moving to the next —
no full-application generation in one step.
