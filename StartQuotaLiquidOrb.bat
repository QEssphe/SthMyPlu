@echo off
set "SCRIPT_DIR=%~dp0"
if not exist "%SCRIPT_DIR%CodexQuota.exe" (
    echo CodexQuota.exe not found. Building it now...
    "%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%build.ps1"
    if errorlevel 1 (
        echo.
        echo Build failed. Please check the error message above.
        pause
        exit /b 1
    )
)
start "" "%SCRIPT_DIR%CodexQuota.exe"
