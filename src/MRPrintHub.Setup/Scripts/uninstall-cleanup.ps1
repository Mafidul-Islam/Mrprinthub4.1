<#
.SYNOPSIS
    MR Print Hub - Silent Uninstaller Cleanup Script
.DESCRIPTION
    Stops and removes the Windows Service and cleans up the Windows Firewall rule.
    Does NOT delete received files in ProgramData unless explicitly requested.
#>

[CmdletBinding()]
param (
    [string]$ServiceName = "MR Print Hub Service",
    [string]$InstallDir = "C:\Program Files\MR Print Hub",
    [switch]$PurgeUserData = $false
)

$ErrorActionPreference = "SilentlyContinue"

# 1. Stop and Delete Windows Service
try {
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
    & sc.exe delete "$ServiceName" | Out-Null
} catch {}

# 2. Remove Windows Firewall Rule
try {
    netsh advfirewall firewall delete rule name="MR Print Hub - Inbound LAN Server" | Out-Null
} catch {}

# 3. Clean up System PATH
try {
    $currentPath = [Environment]::GetEnvironmentVariable("PATH", [EnvironmentVariableTarget]::Machine)
    if ($currentPath -like "*$InstallDir*") {
        $paths = $currentPath -split ';' | Where-Object { $_ -ne $InstallDir -and $_ -ne '' }
        $newPath = $paths -join ';'
        [Environment]::SetEnvironmentVariable("PATH", $newPath, [EnvironmentVariableTarget]::Machine)
    }
} catch {}

# 4. Optional Purge of User Data
if ($PurgeUserData) {
    $ProgramDataPath = "$env:ProgramData\MRPrintHub"
    if (Test-Path $ProgramDataPath) {
        Remove-Item -Path $ProgramDataPath -Recurse -Force -ErrorAction SilentlyContinue
    }
}

exit 0
