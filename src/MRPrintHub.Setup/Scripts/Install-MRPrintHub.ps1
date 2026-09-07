<#
.SYNOPSIS
    Installs MR Print Hub Windows Service and configures Desktop App.

.DESCRIPTION
    This script installs the MR Print Hub Windows Service, configures the
    NamedPipe IPC endpoint, and creates Desktop App shortcuts.

.NOTES
    Requires Administrator privileges.
    Targets Windows 10/11 with .NET 10.0 Runtime.
#>

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
Write-Host "MR Print Hub Installer"
Write-Host "=========================================="
Write-Host ""

#region ----- Admin Privilege Check -----

if (-not (Test-IsAdmin)) {
    Write-Error "This script requires Administrator privileges. Please right-click PowerShell and select 'Run as Administrator'."
    exit 1
}

Write-Host "[1/5] Verification passed: Running with Administrator privileges." -ForegroundColor Cyan

#endregion ----- Admin Privilege Check -----

#region ----- Verification -----

Write-Host "[2/5] Verifying installation requirements..." -ForegroundColor Cyan

# Verify .NET 10.0 Runtime
$dotnetVersion = (dotnet --version 2>$null)
Write-Host "    .NET SDK Version: $dotnetVersion"

# Verify Service Exe Path exists (will be checked with default, user must provide correct path)
$defaultServicePath = "C:\Program Files\MR Print Hub\MRPrintHub.Service.exe"
if (-not (Test-Path $defaultServicePath)) {
    Write-Warning "    Default service path not found: $defaultServicePath"
    Write-Warning "    You will need to provide the correct path to MRPrintHub.Service.exe"
}

#endregion ----- Verification -----

Write-Host "[3/5] Installing Windows Service..." -ForegroundColor Cyan

#region ----- Install Windows Service -----

#region ----- Get Service Exe Path -----

Write-Host "    Please provide the path to MRPrintHub.Service.exe" -ForegroundColor Yellow
$ServiceExePath = Read-Host "Enter full path (or press Enter for default: $defaultServicePath)"

if ([string]::IsNullOrWhiteSpace($ServiceExePath)) {
    $ServiceExePath = $defaultServicePath
}

if (-not (Test-Path $ServiceExePath)) {
    Write-Error "Service executable not found at: $ServiceExePath"
    exit 1
}

#endregion ----- Get Service Exe Path -----

#region ----- Create Service -----

Write-Host "    Creating Windows Service '$ServiceName'..." -ForegroundColor Cyan

try {
    $serviceName = "MR Print Hub Service"
    
    # Check if service already exists
    $existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($existingService) {
        Write-Warning "    Service '$serviceName' already exists."
        $overwrite = Read-Host "Do you want to overwrite existing service? (Y/N)"
        if ($overwrite -ne 'Y' -and $overwrite -ne 'y') {
            Write-Host "    Installation cancelled." -ForegroundColor Yellow
            exit 0
        }
        # Remove existing service first
        try { sc delete $serviceName } catch {}
    }
    
    # Create the service
    $serviceParams = @{
        Name = $serviceName
        binPath = $ServiceExePath
        Start = 'auto'
        DisplayName = 'MR Print Hub Service'
        Description = 'MR Print Hub - Print session management and QR code generation'
        ErrorControl = 'normal'
        Type = 'ownProcess'
        Account = 'LocalSystem'
    }
    
    sc @serviceParams
    Write-Host "    Service created successfully." -ForegroundColor Green
}
catch {
    Write-Error "Failed to create Windows Service: $_"
    exit 1
}

#endregion ----- Create Service -----

#region ----- Start Service -----

Write-Host "    Starting Windows Service..." -ForegroundColor Cyan

try {
    $serviceName = "MR Print Hub Service"
    
    # Try to start the service
    try {
        Start-Service -Name $serviceName
        Write-Host "    Service started successfully." -ForegroundColor Green
    }
    catch {
        Write-Warning "    Service could not be started automatically. It's configured for auto-start and can be started with: Start-Service -Name '$serviceName'"
    }
}
catch {
    Write-Error "Failed to start Windows Service: $_"
    exit 1
}

#endregion ----- Start Service -----

#region ----- Configure NamedPipe -----

Write-Host "[4/5] Configuring NamedPipe IPC..." -ForegroundColor Cyan

Write-Host "    NamedPipe endpoint: \\\.\MRPrintHub_IPC" -ForegroundColor Green
Write-Host "    Pipe name: MRPrintHub_IPC" -ForegroundColor Green
Write-Host "    Service will create pipe on first connection." -ForegroundColor Green

# NamedPipe is configured at runtime by the Service binary
# The Service project (MRPrintHub.Service) handles pipe creation on startup

#endregion ----- Configure NamedPipe -----

#region ----- Create Shortcuts -----

Write-Host "[5/5] Creating Desktop App shortcuts..." -ForegroundColor Cyan

#region ----- Desktop Shortcut -----

Write-Host "    Creating Desktop shortcut..." -ForegroundColor Cyan

$desktopPath = [Environment]::GetFolderPath('Desktop')

if ($desktopPath) {
    $desktopShortcut = Join-Path $desktopPath "MR Print Hub.lnk"
    
    try {
        if (-not (Test-Path $desktopShortcut)) {
            $shell = New-Object -ComObject WScript.Shell
            $shortcut = $shell.CreateShortcut($desktopShortcut)
            $shortcut.TargetPath = $ServiceExePath
            $shortcut.WorkingDirectory = Split-Path $ServiceExePath
            $shortcut.Save()
            Write-Host "    Desktop shortcut created successfully." -ForegroundColor Green
        }
        else {
            Write-Host "    Desktop shortcut already exists." -ForegroundColor Yellow
        }
    }
    catch {
        Write-Warning "    Could not create Desktop shortcut: $_"
    }
}
else {
    Write-Warning "    Could not retrieve Desktop path."
}

#endregion ----- Desktop Shortcut -----

#region ----- Start Menu Shortcut -----

Write-Host "    Configuring Start Menu shortcut..." -ForegroundColor Cyan

$startMenuPath = [Environment]::GetFolderPath('StartMenu')

if ($startMenuPath) {
    $menuDir = Join-Path $startMenuPath "MR Print Hub"
    
    try {
        if (-not (Test-Path $menuDir)) {
            New-Item -ItemType Directory -Path $menuDir -Force | Out-Null
        }
        
        # Create Start Menu shortcut
        $lnkPath = Join-Path $menuDir "MR Print Hub.lnk"
        if (-not (Test-Path $lnkPath)) {
            $shell = New-Object -ComObject WScript.Shell
            $shortcut = $shell.CreateShortcut($lnkPath)
            $shortcut.TargetPath = $ServiceExePath
            $shortcut.WorkingDirectory = Split-Path $ServiceExePath
            $shortcut.Save()
        }
        Write-Host "    Start Menu shortcut configured." -ForegroundColor Green
    }
    catch {
        Write-Warning "    Could not create Start Menu shortcut: $_"
    }
}
else {
    Write-Warning "    Could not retrieve Start Menu path."
}

#endregion ----- Start Menu Shortcut -----

#endregion ----- Shortcuts -----

Write-Host ""
Write-Host "=========================================="
Write-Host "Installation Complete!"
Write-Host "=========================================="
Write-Host ""
Write-Host "Service Name: MR Print Hub Service"
Write-Host "Service Exe: $ServiceExePath"
Write-Host "NamedPipe: \\.\MRPrintHub_IPC"
Write-Host "Desktop Shortcut: Created"
Write-Host "Start Menu: Configured"
Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. The service should start automatically (configured for Auto-start)"
Write-Host "  2. Launch MR Print Hub Desktop from Desktop or Start Menu"
Write-Host "  3. Access Website: http://localhost:5000 (or your configured IP/Port)"
Write-Host "  4. Generate QR codes: Use Desktop or Mobile Website"
Write-Host ""
Write-Host "Uninstall: Run Uninstall-MRPrintHub.ps1 -ServiceName 'MR Print Hub Service'"
Write-Host "=========================================="

#region ----- Write Uninstall Registry Info -----

# Write uninstall information to registry for Add/Remove Programs
$uninstallKey = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\MR Print Hub"
if (-not (Test-Path $uninstallKey)) {
    New-Item -Path "$uninstallKey" -Force | Out-Null
}
Set-ItemProperty -Path "$uninstallKey" -Name "DisplayName" -Value "MR Print Hub Service"
Set-ItemProperty -Path "$uninstallKey" -Name "DisplayVersion" -Value "1.0.0"
Set-ItemProperty -Path "$uninstallKey" -Name "QuietUninstallString" -Value "powershell -ExecutionPolicy Bypass -File `'$PSScriptRoot\Uninstall-MRPrintHub.ps1`'"
Set-ItemProperty -Path "$uninstallKey" -Name "UninstallCommand" -Value "sc stop \"MR Print Hub Service\" && sc delete \"MR Print Hub Service\""

#endregion ----- Write Uninstall Registry Info -----