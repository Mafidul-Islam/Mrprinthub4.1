<#
.SYNOPSIS
    Uninstalls MR Print Hub Windows Service and removes Desktop App shortcuts.

.DESCRIPTION
    This script uninstalls the MR Print Hub Windows Service, removes
    Desktop App shortcuts, and cleans up registry entries.

.NOTES
    Requires Administrator privileges.
#endregion

#region ----- Get-IsAdmin Helper Function -----

#region P/Invoke Signatures
[DllImport("shell32.dll", SetLastError = true)]
static extern bool IsUserAnAdmin();
#endregion

function Test-IsAdmin {
    # Use the P/Invoke method for reliable admin check
    return [bool]::new(IsUserAnAdmin())
}

#endregion ----- Get-IsAdmin Helper Function -----

Write-Host "=========================================="
Write-Host "MR Print Hub Uninstaller"
Write-Host "=========================================="
Write-Host ""

#region ----- Admin Privilege Check -----

if (-not (Test-IsAdmin)) {
    Write-Error "This script requires Administrator privileges. Please right-click PowerShell and select 'Run as Administrator'."
    exit 1
}

Write-Host "[1/4] Verification passed: Running with Administrator privileges." -ForegroundColor Cyan

#endregion ----- Admin Privilege Check -----

#region ----- Stop and Remove Windows Service -----

Write-Host "[2/4] Stopping and removing Windows Service..." -ForegroundColor Cyan

#region ----- Stop Service -----

try {
    $serviceName = "MR Print Hub Service"
    
    # Try to stop the service if running
    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($service -and ($service.Status -eq "Running" -or $service.Status -eq "StartPending" -or $service.Status -eq "StopPending")) {
        Write-Host "    Stopping service..." -ForegroundColor Yellow
        Stop-Service -Name $serviceName -Force
        Write-Host "    Service stopped." -ForegroundColor Green
    }
    else {
        Write-Host "    Service is not running. Proceeding with deletion." -ForegroundColor Green
    }
}
catch {
    Write-Warning "    Could not check service status: $_"
}

#endregion ----- Stop Service -----

#region ----- Delete Service -----

try {
    Write-Host "    Deleting service..." -ForegroundColor Cyan
    sc delete "$serviceName"
    Write-Host "    Service deleted successfully." -ForegroundColor Green
}
catch {
    Write-Error "Failed to remove Windows Service: $_"
    exit 1
}

#endregion ----- Delete Service -----

#endregion ----- Service Removal -----

#region ----- Remove Shortcuts -----

Write-Host "[3/4] Removing Desktop App shortcuts..." -ForegroundColor Cyan

#region ----- Remove Desktop Shortcut -----

$desktopPath = [Environment]::GetFolderPath('Desktop')
if ($desktopPath) {
    $desktopShortcut = Join-Path $desktopPath "MR Print Hub.lnk"
    if (Test-Path $desktopShortcut) {
        Remove-Item -Path $desktopShortcut -Force
        Write-Host "    Desktop shortcut removed." -ForegroundColor Green
    }
    else {
        Write-Host "    Desktop shortcut not found (already removed)." -ForegroundColor Yellow
    }
}
else {
    Write-Warning "    Could not retrieve Desktop path."
}

#endregion ----- Remove Desktop Shortcut -----

#region ----- Remove Start Menu Shortcuts -----

Write-Host "    Removing Start Menu entries..." -ForegroundColor Cyan

$startMenuPath = [Environment]::GetFolderPath('StartMenu')
if ($startMenuPath) {
    $menuPath = Join-Path $startMenuPath "MR Print Hub"
    if (Test-Path $menuPath) {
        Remove-Item -Path $menuPath -Recurse -Force
        Write-Host "    Start Menu entries removed." -ForegroundColor Green
    }
    else {
        Write-Host "    Start Menu entries not found (already removed)." -ForegroundColor Yellow
    }
}
else {
    Write-Warning "    Could not retrieve Start Menu path."
}

#endregion ----- Remove Start Menu Shortcuts -----

#endregion ----- Shortcut Removal -----

#region ----- Remove Registry Entries -----

Write-Host "[4/4] Removing registry entries..." -ForegroundColor Cyan

#region ----- Remove Uninstall Registry Key -----

$uninstallKey = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\MR Print Hub"
if (Test-Path $uninstallKey) {
    Remove-Item -Path $uninstallKey -Force
    Write-Host "    Uninstall registry key removed." -ForegroundColor Green
}
else {
    Write-Host "    Uninstall registry key not found (already removed)." -ForegroundColor Yellow
}

#endregion ----- Remove Uninstall Registry Key -----

#region ----- Remove MR Print Hub Registry Key -----

$hrKey = "HKLM:\Software\MR Print Hub"
if (Test-Path $hrKey) {
    Remove-Item -Path $hrKey -Force
    Write-Host "    MR Print Hub registry key removed." -ForegroundColor Green
}
else {
    Write-Host "    MR Print Hub registry key not found (already removed)." -ForegroundColor Yellow
}

#endregion ----- Remove MR Print Hub Registry Key -----

#endregion ----- Registry Removal -----

Write-Host ""
Write-Host "=========================================="
Write-Host "Uninstallation Complete!"
Write-Host "=========================================="
Write-Host ""
Write-Host "MR Print Hub has been completely removed from this system."
Write-Host "To reinstall, run: Install-MRPrintHub.ps1"
Write-Host "=========================================="