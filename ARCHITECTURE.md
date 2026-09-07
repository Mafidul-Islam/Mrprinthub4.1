# MR Print Hub — Architecture

## 1. Overview

MR Print Hub lets a shop's Windows PC receive files from a customer's phone
over the local Wi-Fi/LAN, with no app, account, or internet required on the
phone side. The customer scans a QR code, lands on a mobile web page served
directly by the PC, and uploads files that land in a configured folder.

Two long-running processes cooperate on the PC:

- **MRPrintHub.Service** — a Windows Service that owns the HTTP server, the
  database, network monitoring, and all upload logic. This is the thing that
  must survive logoff/reboot and keep running "in the background."
- **MRPrintHub.Desktop** — a WPF tray/dashboard app that the shop owner sees
  and interacts with. It never talks to the filesystem or network directly;
  it talks to the Service.

Keeping these separate (rather than one WPF app doing everything) is the
safest production choice: a Windows Service can run without a logged-in user
session, restart independently of the desktop UI, and is the correct place to
host a network listener with elevated lifetime guarantees. The desktop app
becomes a thin, replaceable UI shell — which also means WPF could later be
swapped for WinUI 3 without touching the server/security code at all.

## 2. High-Level Diagram

```
 [Customer's Phone Browser]
          | HTTP (LAN only)
          v
 +-------------------------+
 | MRPrintHub.Service       |
 |  - Kestrel HTTP server   |<--- MRPrintHub.Server (ASP.NET Core minimal API)
 |  - Session/token mgmt    |<--- MRPrintHub.Security
 |  - Upload pipeline       |<--- MRPrintHub.Storage
 |  - Network watcher       |<--- MRPrintHub.Network
 |  - SQLite via EF Core    |<--- MRPrintHub.Database
 |  - QR generation         |<--- MRPrintHub.QR
 |  - Named-pipe IPC server |
 +-------------------------+
          ^ named pipes / local IPC (no network exposure)
          |
 +-------------------------+
 | MRPrintHub.Desktop (WPF) |
 |  - Dashboard, Wizard      |
 |  - Tray icon              |
 |  - Calls Service via IPC  |
 +-------------------------+
```

`MRPrintHub.Core` holds shared contracts (DTOs, interfaces, enums) referenced
by both the Service and Desktop so they never need to share concrete types
across the IPC boundary.

## 3. Communication: Desktop ↔ Service

The Desktop app is *not* the server and must not open its own network
listener. It talks to the Service exclusively over a local **named pipe**
(`net.pipe` binding or `System.IO.Pipes` with a small JSON-RPC-style
protocol). This is deliberate:

- Named pipes are local-machine-only by default — no accidental LAN exposure
  of a management API.
- The dashboard's "live" data (status, IP, storage usage, recent uploads)
  is pushed from Service to Desktop over the same pipe using a simple
  pub/sub notification message, so the UI doesn't need to poll SQLite
  directly (avoids file-lock contention with the Service).

If the Service isn't running, the Desktop shows a clear "Service offline —
uploads are not accepting" state and offers a "Start Service" action
(requires admin elevation, handled via a UAC prompt through
`ServiceController`).

## 4. Request Flow (Customer Upload)

1. Service starts → `NetworkMonitor` detects active LAN/Wi-Fi adapter IP.
2. `SessionManager` creates a session: random 256-bit token, expiry timestamp,
   bound to the current IP+port.
3. `QrService` renders `http://{ip}:{port}/u/{token}` as a QR PNG; Desktop
   displays it.
4. Phone scans QR → GET `/u/{token}` → server validates token (exists, not
   expired, IP still matches the interface it was issued for) → serves the
   mobile upload SPA (static files, no build step needed — plain HTML/CSS/JS).
5. Phone POSTs file(s) to `/api/upload/{token}` as multipart/form-data.
6. Server pipeline per file:
   - Reject if session invalid/expired → 401 with a typed error the UI maps
     to "Session expired."
   - Reject if content-length exceeds configured max before reading body.
   - Enforce a per-session upload semaphore (concurrency limit) with
     `SemaphoreSlim`.
   - Stream (not buffer fully in memory) to `Temp/{sessionId}/{guid}.part`.
   - Validate actual bytes read against limit while streaming (defends
     against chunked-encoding lies about size).
   - Sanitize filename (strip path separators, reserved Windows device names
     like `CON`/`PRN`, control characters; enforce extension allow-list).
   - Validate extension against configured allow-list; optionally sniff magic
     bytes for common types as a secondary check (do not trust
     `Content-Type` header alone).
   - On success, atomically move (`File.Move`, same volume) from `Temp/` to
     `Received/`, resolving filename collisions with a `(1)`, `(2)`... suffix.
   - Record an `Uploads` row; emit a desktop notification and an IPC event to
     the Desktop app.
   - On any failure, delete the partial temp file and return a typed error.
7. Low-disk-space guard runs before accepting new uploads (configurable
   threshold, default e.g. 500 MB free).

## 5. Security Model

- **Local-network-only enforcement**: Kestrel binds only to the detected
  local adapter's IPv4 address (not `0.0.0.0`), so the server is unreachable
  from outside that adapter's subnet. This is defense-in-depth on top of the
  Windows Firewall rule created by the installer, which itself should be
  scoped to Private/Domain profiles only (never Public).
- **Tokens**: `RandomNumberGenerator`-based 256-bit tokens, URL-safe base64.
  Never sequential IDs.
- **Session lifecycle**: sessions expire after a configurable TTL (default 30
  min of inactivity) and are invalidated immediately on any of: IP change,
  adapter change, service restart, manual "Regenerate QR" action.
- **No filesystem traversal**: the HTTP server exposes exactly two routes
  that touch disk (`GET /u/{token}` static SPA, `POST /api/upload/{token}`).
  There is no generic file-serving route, no directory listing, and the
  configured storage folder path is never accepted as user input.
- **No execution surface**: uploaded files are never executed, opened, or
  passed to a shell. They are inert bytes on disk.
- **Rate limiting**: ASP.NET Core rate limiting middleware, keyed by session
  token, to blunt abuse from a single phone.
- **Logging discipline**: structured logs (Serilog) capture event type,
  timestamp, sanitized filename, size, and outcome — never file contents,
  never full absolute paths beyond what's needed for support, never raw
  tokens (log a short hash/prefix instead).

## 6. Data Model (SQLite, EF Core Migrations)

- `Settings` — key/value app configuration (storage path, size limits,
  allowed extensions, TTLs, port range, low-disk threshold).
- `Sessions` — id, token (hashed at rest), created_at, expires_at, ip,
  interface_name, revoked flag.
- `Uploads` — id, session_id (FK), original_filename, stored_filename, size,
  status (pending/complete/failed), created_at, completed_at, error_reason.
- `ApplicationLogs` — optional DB-backed log sink for the in-app "Logs"
  screen (in addition to file-based Serilog logs), with retention/pruning.

No customer-identifying data is ever collected (no names, no phone info,
nothing beyond what's necessary for the upload record).

## 7. Network Resilience

`MRPrintHub.Network` hosts a `NetworkChange`-based watcher
(`NetworkAvailabilityChanged`, `NetworkAddressChanged`) that:

- Re-resolves the active adapter/IP on any change.
- Invalidates the current session/QR and generates a new one when the bound
  IP changes (DHCP renewal, router restart, Wi-Fi reconnect).
- Pauses the "online" status shown on the dashboard when no suitable
  local adapter is found, and resumes automatically when one reappears.
- On PC sleep/wake (`SystemEvents.PowerModeChanged`), forces a fresh network
  re-check rather than trusting stale adapter state.
- Virtual/VPN adapters are explicitly deprioritized using heuristics
  (interface type, description keywords, and preferring adapters with a
  default gateway) so a VPN doesn't get selected as the "shop network."

## 8. Deployment & Lifecycle

- **Installer** (WiX Toolset): installs Service (auto-start), Desktop app,
  creates `ProgramData\MRPrintHub` for storage/config/db (writable location,
  not Program Files), adds a scoped Windows Firewall inbound rule for the
  configured port, Start Menu shortcut, optional desktop shortcut, and
  registers Desktop app to launch at user logon (tray only, not the Service —
  the Service already auto-starts via SCM).
- **Uninstall**: stops/removes the Service, removes firewall rule, removes
  Start Menu/startup entries; a checkbox lets the shop owner keep or delete
  `ProgramData\MRPrintHub` (received files) — defaulting to *keep*, since
  deleting customer files silently would be harmful.
- **Update mechanism (future)**: a version-check endpoint the Desktop app
  polls; actual auto-update is out of scope for v1 and will be designed once
  core functionality is stable.
- **Licensing (future)**: `MRPrintHub.Core` will define an
  `ILicenseValidator` interface with a no-op/free implementation for v1, so a
  real licensing backend can be substituted later without touching business
  logic.

## 9. Why this stack

- **.NET 8 (LTS) + WPF**: mature, first-class Windows Service support, long
  support window.
- **ASP.NET Core (Kestrel)**: production-grade HTTP server, built-in support
  for streaming uploads, rate limiting, and minimal APIs — avoids hand-rolling
  an HTTP listener.
- **SQLite + EF Core**: zero-install embedded database, appropriate for a
  single-PC desktop app; migrations keep schema changes safe across updates.
- **QRCoder**: mature, dependency-light QR generation.
- **Serilog**: structured logging with file sinks and easy redaction of
  sensitive fields.

## 10. Testing Strategy

Unit tests target pure logic with no I/O: token generation, filename
sanitization, extension validation, session expiry rules. Integration tests
spin up the ASP.NET Core server with `WebApplicationFactory` against a
temp folder and temp SQLite file to exercise real upload flows, size/
extension rejection, path-traversal attempts, duplicate filenames, and
concurrent uploads — see `IMPLEMENTATION_PLAN.md` for the phase where each
suite lands.
