# MR Print Hub — Production Packaging & Installer Guide

## Overview
This setup provides an end-to-end DevOps packaging pipeline to build a **single standalone setup executable** (`MRPrintHub-Setup.exe`).

### End-User Experience:
1. Double-click `MRPrintHub-Setup.exe` (Windows UAC prompts for Admin elevation automatically).
2. Click **Next** → **Install** → **Finish**.
3. Background services (`MRPrintHub.Service`), directory permissions (`ProgramData\MRPrintHub`), Windows Firewall rules (Port 5000 LAN access), and desktop shortcuts are configured silently without any black terminal popups or manual terminal commands.
4. On the final page, leaving **"Launch MR Print Hub"** checked immediately launches the dashboard.

---

## Packaging Architecture

| Component | Function |
|---|---|
| `MRPrintHub-Setup.iss` | Inno Setup configuration bundling all published binaries, data folders, shortcuts, and background setup scripts into a single `.exe`. |
| `Scripts/setup-dependencies.ps1` | Executed silently during setup (`-WindowStyle Hidden`) to configure `ProgramData` ACLs, add Windows Firewall rule for port 5000, and register/start `MRPrintHub.Service`. |
| `Scripts/uninstall-cleanup.ps1` | Executed during uninstall to cleanly stop/remove the service and firewall rule while preserving customer files in `ProgramData\MRPrintHub\Received`. |
| `build-installer.ps1` / `build-installer.bat` | One-click automated build pipeline: compiles the .NET solution (self-contained win-x64) and builds `dist\MRPrintHub-Setup.exe`. |

---

## Step-by-Step Compilation Guide

### Prerequisites (on build machine):
1. **.NET SDK** (.NET 8.0 or .NET 10.0)
2. **Inno Setup 6** (Free from [jrsoftware.org](https://jrsoftware.org/isdl.php))

### Build the Installer (1-Click):
1. Open PowerShell or Command Prompt in `src\MRPrintHub.Setup\`.
2. Run:
   ```cmd
   build-installer.bat
   ```
   *or in PowerShell:*
   ```powershell
   .\build-installer.ps1
   ```

3. The final single installer will be generated at:
   ```
   dist\MRPrintHub-Setup.exe
   ```
