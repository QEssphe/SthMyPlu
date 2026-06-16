# Codex Quota Liquid Orb

A small Windows floating widget that shows Codex used quota percentages and reset times in a compact Apple-style progress panel. When dragged to a screen edge, it docks smoothly and leaves a green liquid tail that visualizes the 5-hour used quota percentage.

[中文说明](README.zh-CN.md)

## Features

- Live Codex quota polling through the local Codex `app-server` transport.
- Shows 5-hour used quota, weekly used quota, reset time, and countdown.
- Borderless rounded WinForms window with smooth drag and dock animations.
- Edge docking on the top, bottom, left, and right sides of the active monitor.
- Docked green liquid tail that displays the 5-hour used quota percentage.
- Right-click menu for details, refresh, and exit.
- Windows tray icon with show, hide, refresh, and exit actions.
- Remembers window position and dock state locally.

## Requirements

- Windows 10 or later.
- PowerShell 5.1 for building with `Add-Type`.
- .NET Framework WinForms support, included with normal Windows desktop installations.
- Node.js available in `PATH`, or set `NODE_EXE` to the full `node.exe` path.
- Codex desktop/CLI installation available locally.

The quota poller discovers Codex automatically from common OpenAI Codex install locations. If it cannot find Codex, set:

```powershell
$env:CODEX_EXE = "C:\Path\To\codex.exe"
```

## Quick Start

Build the executable:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

Run the app:

```powershell
.\CodexQuota.exe
```

Or double-click:

```text
StartQuotaLiquidOrb.bat
```

The repository also includes a prebuilt `CodexQuota.exe` for quick testing. If the executable is missing, the batch file will build it automatically on first launch, then start the app.

## Usage

- Drag with the left mouse button to move the widget.
- Drag near a monitor edge and release to dock it.
- Click the green docked tail to expand the full panel.
- Percent values are used quota, not remaining quota.
- Double-click the panel to show detailed quota information.
- Right-click the panel or tray icon for actions.

## Files

- `CodexQuotaApp.cs` - WinForms desktop widget source.
- `CodexQuota.exe` - Prebuilt Windows executable for quick testing.
- `quota-poller.mjs` - Long-running Node.js poller that reads Codex quota data.
- `quota-probe.mjs` - One-shot quota probe for debugging.
- `build.ps1` - Builds `CodexQuota.exe`.
- `StartQuotaLiquidOrb.bat` - Starts the built executable.

Runtime files are intentionally ignored by git:

- `quota-live.json`
- `orb-window.json`

## Debugging

Run a one-shot quota probe:

```powershell
node .\quota-probe.mjs .\quota-live.json
Get-Content .\quota-live.json
```

If Node or Codex is installed in a custom location:

```powershell
$env:NODE_EXE = "C:\Path\To\node.exe"
$env:CODEX_EXE = "C:\Path\To\codex.exe"
.\CodexQuota.exe
```

## Notes

This project talks only to the local Codex app-server process. It does not ask for or store GitHub credentials, OpenAI passwords, or API keys.
