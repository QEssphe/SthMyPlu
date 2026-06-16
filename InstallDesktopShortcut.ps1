$ErrorActionPreference = "Stop"

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $projectDir "CodexQuota.exe"
$icon = Join-Path $projectDir "CodexQuota.ico"
$desktop = [Environment]::GetFolderPath("DesktopDirectory")
$shortcutName = "Codex " + [char]0x989D + [char]0x5EA6 + ".lnk"
$shortcutPath = Join-Path $desktop $shortcutName

if (-not (Test-Path -LiteralPath $exe)) {
    $build = Join-Path $projectDir "build.ps1"
    if (-not (Test-Path -LiteralPath $build)) {
        throw "CodexQuota.exe was not found, and build.ps1 is missing."
    }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $build
}

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $projectDir
$shortcut.Description = "Codex quota used percentage widget"
if (Test-Path -LiteralPath $icon) {
    $shortcut.IconLocation = "$icon,0"
}
$shortcut.Save()

Write-Host "Created desktop shortcut: $shortcutPath"
