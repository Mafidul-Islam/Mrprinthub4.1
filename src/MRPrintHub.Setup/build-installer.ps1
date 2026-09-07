<#
.SYNOPSIS
    Automated Single-Step Build & Package Script for MR Print Hub Installer
.DESCRIPTION
    1. Cleans previous build and publish artifacts.
    2. Builds and publishes MRPrintHub.Service and MRPrintHub.Desktop (self-contained win-x64).
    3. Invokes Inno Setup Compiler (ISCC.exe) to generate dist\MRPrintHub-Setup.exe.
#>

[CmdletBinding()]
param (
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [bool]$SelfContained = $true
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionRoot = Resolve-Path (Join-Path $ScriptDir "..\..")
$PublishDir = Join-Path $ScriptDir "publish"
$DistDir = Join-Path $SolutionRoot "dist"
$IssFile = Join-Path $ScriptDir "MRPrintHub-Setup.iss"
$IcoFile = Join-Path $ScriptDir "MRPrintHub.ico"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " MR Print Hub - Installer Builder" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Solution Root: $SolutionRoot"
Write-Host "Publish Dir:   $PublishDir"
Write-Host "Dist Dir:      $DistDir"
Write-Host ""

# -------------------------------------------------------------------------
# 1. Clean Previous Artifacts
# -------------------------------------------------------------------------
Write-Host "[1/4] Cleaning previous publish and dist directories..." -ForegroundColor Yellow
if (Test-Path $PublishDir) { Remove-Item -Path $PublishDir -Recurse -Force }
if (Test-Path $DistDir) { Remove-Item -Path $DistDir -Recurse -Force }
New-Item -Path $PublishDir -ItemType Directory -Force | Out-Null
New-Item -Path $DistDir -ItemType Directory -Force | Out-Null

# -------------------------------------------------------------------------
# 2. Publish MRPrintHub.Service (Background Service Host & Server)
# -------------------------------------------------------------------------
Write-Host "[2/4] Publishing MRPrintHub.Service ($Configuration | $Runtime | SelfContained: $SelfContained)..." -ForegroundColor Green
$serviceCsproj = Join-Path $SolutionRoot "src\MRPrintHub.Service\MRPrintHub.Service.csproj"
& dotnet publish "$serviceCsproj" `
    -c $Configuration `
    -r $Runtime `
    --self-contained $SelfContained `
    -o "$PublishDir"
if ($LASTEXITCODE -ne 0) { throw "Failed to publish MRPrintHub.Service" }

# -------------------------------------------------------------------------
# 3. Publish MRPrintHub.Desktop (WPF Client App)
# -------------------------------------------------------------------------
Write-Host "[3/4] Publishing MRPrintHub.Desktop ($Configuration | $Runtime | SelfContained: $SelfContained)..." -ForegroundColor Green
$desktopCsproj = Join-Path $SolutionRoot "src\MRPrintHub.Desktop\MRPrintHub.Desktop.csproj"
& dotnet publish "$desktopCsproj" `
    -c $Configuration `
    -r $Runtime `
    --self-contained $SelfContained `
    -o "$PublishDir"
if ($LASTEXITCODE -ne 0) { throw "Failed to publish MRPrintHub.Desktop" }

# Copy ICO file into publish folder
if (Test-Path $IcoFile) {
    Copy-Item $IcoFile (Join-Path $PublishDir "MRPrintHub.ico") -Force
}

# -------------------------------------------------------------------------
# 4. Locate Inno Setup Compiler (ISCC.exe) and Compile Installer
# -------------------------------------------------------------------------
Write-Host "[4/4] Compiling Single-File Setup Executable with Inno Setup..." -ForegroundColor Green

$isccCandidates = @(
    "ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)

$isccPath = $null
foreach ($candidate in $isccCandidates) {
    if (Get-Command $candidate -ErrorAction SilentlyContinue) {
        $isccPath = $candidate
        break
    }
    if (Test-Path $candidate) {
        $isccPath = $candidate
        break
    }
}

if (-not $isccPath) {
    Write-Warning "Inno Setup Compiler (ISCC.exe) was not found in standard paths."
    Write-Warning "Please verify Inno Setup 6 is installed, or compile '$IssFile' directly."
    Write-Host ""
    Write-Host "Published binaries are ready in: $PublishDir" -ForegroundColor Yellow
    exit 0
}

Write-Host "Using Inno Setup Compiler: $isccPath" -ForegroundColor DarkCyan
& "$isccPath" "$IssFile"
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed" }

$outputInstaller = Join-Path $DistDir "MRPrintHub-Setup.exe"
if (Test-Path $outputInstaller) {
    $sizeMb = [math]::Round((Get-Item $outputInstaller).Length / 1MB, 2)
    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Green
    Write-Host " SUCCESS! Single installer created:" -ForegroundColor Green
    Write-Host " $outputInstaller ($sizeMb MB)" -ForegroundColor Green
    Write-Host "============================================================" -ForegroundColor Green
}
