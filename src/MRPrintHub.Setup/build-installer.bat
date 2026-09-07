@echo off
setlocal
cd /d "%~dp0"
echo ========================================================
echo Starting MR Print Hub Automated Packaging Pipeline
echo ========================================================
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-installer.ps1"
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Packaging failed with exit code %ERRORLEVEL%.
    pause
    exit /b %ERRORLEVEL%
)
echo.
echo Packaging completed successfully!
pause
