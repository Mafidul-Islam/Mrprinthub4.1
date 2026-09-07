<#
.SYNOPSIS
    MR Print Hub - Automated Background Dependency & Environment Setup
.DESCRIPTION
    Executed silently during installer execution to configure prerequisites,
    directory permissions, Windows Firewall rules, and the Windows Service.
    Runs silently with no user prompts or terminal popups.
#>

[CmdletBinding()]
param (
    [string]$InstallDir = "C:\Program Files\MR Print Hub",
    [int]$Port = 5000,
    [string]$ServiceName = "MR Print Hub Service",
    [switch]$SkipRuntimeCheck
)

$ErrorActionPreference = "SilentlyContinue"

# -------------------------------------------------------------------------
# 1. Directory Structure & Permissions Setup
# -------------------------------------------------------------------------
$ProgramDataPath = "$env:ProgramData\MRPrintHub"
$SubDirs = @("Received", "Temp", "Logs", "Database")

foreach ($dir in $SubDirs) {
    $target = Join-Path $ProgramDataPath $dir
    if (-not (Test-Path $target)) {
        New-Item -Path $target -ItemType Directory -Force | Out-Null
    }
}

# Grant Full Control to Users & Everyone on ProgramData\MRPrintHub
try {
    $acl = Get-Acl $ProgramDataPath
    $ruleUsers = New-Object System.Security.AccessControl.FileSystemAccessRule("Users", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")
    $ruleEveryone = New-Object System.Security.AccessControl.FileSystemAccessRule("Everyone", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")
    $acl.SetAccessRule($ruleUsers)
    $acl.SetAccessRule($ruleEveryone)
    Set-Acl -Path $ProgramDataPath -AclObject $acl
} catch {
    # Fallback to icacls if PowerShell ACL manipulation fails
    & icacls "$ProgramDataPath" /grant "*S-1-5-32-545:(OI)(CI)F" /T /Q | Out-Null
}

# -------------------------------------------------------------------------
# 2. Windows Firewall Rule Configuration (Inbound TCP Port 5000 LAN)
# -------------------------------------------------------------------------
$FirewallRuleName = "MR Print Hub - Inbound LAN Server"

# Remove any existing rule first to ensure clean state
try {
    netsh advfirewall firewall delete rule name="$FirewallRuleName" | Out-Null
} catch {}

try {
    # Add scoped rule for Private and Domain networks
    netsh advfirewall firewall add rule `
        name="$FirewallRuleName" `
        dir=in `
        action=allow `
        protocol=TCP `
        localport=$Port `
        profile=private,domain `
        description="Allows incoming mobile browser uploads to MR Print Hub on local Wi-Fi/LAN" | Out-Null
} catch {}

# -------------------------------------------------------------------------
# 3. Windows Service Registration & Auto-Start
# -------------------------------------------------------------------------
$ServiceExe = Join-Path $InstallDir "MRPrintHub.Service.exe"

if (Test-Path $ServiceExe) {
    # Check if service already exists
    $existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($existing) {
        try {
            Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 1
            & sc.exe delete "$ServiceName" | Out-Null
            Start-Sleep -Seconds 1
        } catch {}
    }

    # Create Service using sc.exe
    $binPathWithQuotes = "`"$ServiceExe`""
    & sc.exe create "$ServiceName" binPath= $binPathWithQuotes start= auto DisplayName= "MR Print Hub Service" obj= "LocalSystem" | Out-Null
    & sc.exe description "$ServiceName" "Background HTTP listener and IPC print hub coordinator for MR Print Hub" | Out-Null

    # Configure recovery actions (restart on failure)
    & sc.exe failure "$ServiceName" reset= 86400 actions= restart/5000/restart/10000/restart/60000 | Out-Null

    # Start the service
    try {
        Start-Service -Name $ServiceName -ErrorAction SilentlyContinue
    } catch {}
}

# -------------------------------------------------------------------------
# 4. System Environment PATH (ensure InstallDir is in PATH if needed)
# -------------------------------------------------------------------------
try {
    $currentPath = [Environment]::GetEnvironmentVariable("PATH", [EnvironmentVariableTarget]::Machine)
    if ($currentPath -notlike "*$InstallDir*") {
        $newPath = "$currentPath;$InstallDir"
        [Environment]::SetEnvironmentVariable("PATH", $newPath, [EnvironmentVariableTarget]::Machine)
    }
} catch {}

exit 0
