<#
.SYNOPSIS
    MR Print Hub Installation Scripts

.DESCRIPTION
    Collection of PowerShell scripts for installing and uninstalling
    the MR Print Hub Windows Service and Desktop App.

.INSTALLATION
    Installer:        Install-MRPrintHub.ps1
    Uninstaller:      Uninstall-MRPrintHub.ps1

.REQUIREMENTS
    - Administrator privileges
    - .NET 10.0 Runtime
    - Windows 10/11
#>

Export-ModuleMember -Function Get-IsAdmin, Install-MRPrintHub, Uninstall-MRPrintHub

function Get-IsAdmin {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    return $principal.IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
}

function Install-MRPrintHub {
    param(
        [Parameter(Mandatory=$true)]
        [string]$ServiceName = "MR Print Hub Service",

        [Parameter(Mandatory=$true)]
        [string]$ServiceExePath,

        [Parameter(Mandatory=$true)]
        [string]$PipeName = "MRPrintHub_IPC",

        [Parameter(Mandatory=$false)]
        [switch]$CreateShortcuts = $true,

        [Parameter(Mandatory=$false)]
        [switch]$EnableService = $true
    )

    # Call the main installer logic
    . '.\Install-MRPrintHub.ps1' -ServiceName $ServiceName -ServiceExePath $ServiceExePath -PipeName $PipeName -CreateShortcuts $CreateShortcuts -EnableService $EnableService
}

function Uninstall-MRPrintHub {
    param(
        [Parameter(Mandatory=$true)]
        [string]$ServiceName = "MR Print Hub Service",

        [Parameter(Mandatory=$false)]
        [switch]$Force = $false
    )

    # Call the main uninstaller logic
    . '.\Uninstall-MRPrintHub.ps1' -ServiceName $ServiceName
}