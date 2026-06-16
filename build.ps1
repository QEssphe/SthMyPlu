$ErrorActionPreference = "Stop"

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $projectDir "CodexQuotaApp.cs"
$output = Join-Path $projectDir "CodexQuota.exe"

if (-not (Test-Path -LiteralPath $source)) {
    throw "Source file not found: $source"
}

if (Test-Path -LiteralPath $output) {
    Remove-Item -LiteralPath $output -Force
}

Add-Type `
    -Path $source `
    -ReferencedAssemblies System.Windows.Forms,System.Drawing `
    -OutputAssembly $output `
    -OutputType WindowsApplication

Write-Host "Built $output"
